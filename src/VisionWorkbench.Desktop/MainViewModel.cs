using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VisionWorkbench.Core;

namespace VisionWorkbench.Desktop;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ImageProcessor _processor = new();
    private byte[]? _input;
    private ProcessingResult? _result;
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private int _threshold = 128;
    private double _minimumArea = 500;
    private bool _invert;
    private bool _blur;
    private string _status = "加载示例图或打开本地图片，开始一次检测。";
    private string _fileName = "尚未加载图片";
    private string _dimensions = "—";
    private string _duration = "—";
    private BitmapSource? _original;
    private BitmapSource? _binary;
    private BitmapSource? _annotated;

    public MainViewModel()
    {
        LoadSampleCommand = new AsyncCommand(LoadSampleAsync, () => !IsBusy);
        OpenCommand = new AsyncCommand(OpenAsync, () => !IsBusy);
        ProcessCommand = new AsyncCommand(ProcessAsync, () => !IsBusy && _input is not null);
        SaveCommand = new AsyncCommand(SaveAsync, () => !IsBusy && CanSave);
        CancelCommand = new AsyncCommand(() => { _cancellation?.Cancel(); return Task.CompletedTask; }, () => _cancellation is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand LoadSampleCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ProcessCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ObservableCollection<ContourMeasurement> Measurements { get; } = [];
    public ProcessingResult? CurrentResult => _result;
    public bool IsBusy { get => _busy; private set { Set(ref _busy, value); Notify(nameof(IsIdle)); RefreshCommands(); } }
    public bool IsIdle => !IsBusy;
    public int Threshold { get => _threshold; set { if (Set(ref _threshold, value)) ParametersChanged(); } }
    public double MinimumArea { get => _minimumArea; set { if (Set(ref _minimumArea, value)) ParametersChanged(); } }
    public bool Invert { get => _invert; set { if (Set(ref _invert, value)) ParametersChanged(); } }
    public bool Blur { get => _blur; set { if (Set(ref _blur, value)) ParametersChanged(); } }
    public bool CanSave => _result is not null && _result.Parameters == SnapshotParameters();
    public string ResultState => _result is null ? "等待检测" : CanSave ? "结果已更新" : "参数已改变 · 请重新检测";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string FileName { get => _fileName; private set => Set(ref _fileName, value); }
    public string Dimensions { get => _dimensions; private set => Set(ref _dimensions, value); }
    public string Duration { get => _duration; private set => Set(ref _duration, value); }
    public int ContourCount => Measurements.Count;
    public BitmapSource? OriginalImage { get => _original; private set => Set(ref _original, value); }
    public BitmapSource? BinaryImage { get => _binary; private set => Set(ref _binary, value); }
    public BitmapSource? AnnotatedImage { get => _annotated; private set => Set(ref _annotated, value); }

    public async Task LoadSampleAsync()
    {
        if (IsBusy) return;
        await ExecuteProcessingAsync(() => SampleImages.CreateShapes(), "合成示例 · 三种几何形状.png");
    }

    public async Task LoadFileAsync(string path)
    {
        if (IsBusy) return;
        await ExecuteProcessingAsync(() =>
        {
            var information = new FileInfo(path);
            if (information.Length > ImageProcessor.MaximumFileBytes)
                throw new InvalidDataException("图片超过 32 MB，请先缩小文件。");
            return File.ReadAllBytes(path);
        }, Path.GetFileName(path));
    }

    private Task OpenAsync()
    {
        var dialog = new OpenFileDialog { Title = "打开待处理图片", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*" };
        return dialog.ShowDialog() == true ? LoadFileAsync(dialog.FileName) : Task.CompletedTask;
    }

    public Task ProcessAsync()
    {
        if (IsBusy || _input is null) return Task.CompletedTask;
        var snapshot = _input;
        return ExecuteProcessingAsync(() => snapshot, FileName);
    }

    private async Task ExecuteProcessingAsync(Func<byte[]> load, string name)
    {
        var parameters = SnapshotParameters();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        IsBusy = true;
        Status = "正在处理图片…";
        try
        {
            var output = await Task.Run(() =>
            {
                var input = load();
                var result = _processor.Process(input, parameters, token);
                return (Input: input, Result: result,
                    Original: ImageConversion.FromPng(result.OriginalPng),
                    Binary: ImageConversion.FromPng(result.BinaryPng),
                    Annotated: ImageConversion.FromPng(result.AnnotatedPng));
            }, token);
            token.ThrowIfCancellationRequested();
            _input = output.Input;
            _result = output.Result;
            FileName = name;
            OriginalImage = output.Original;
            BinaryImage = output.Binary;
            AnnotatedImage = output.Annotated;
            Measurements.Clear();
            foreach (var measurement in output.Result.Measurements) Measurements.Add(measurement);
            Dimensions = $"{output.Result.Width} × {output.Result.Height}";
            Duration = $"{output.Result.ElapsedMilliseconds:F1} ms";
            Notify(nameof(ContourCount));
            Notify(nameof(CanSave));
            Notify(nameof(ResultState));
            Status = $"处理完成，保留 {ContourCount} 个外轮廓。面积与周长均为像素单位。";
        }
        catch (OperationCanceledException) { Status = "本次处理已取消，保留上一次结果。"; }
        catch (Exception exception) { Status = $"处理失败：{exception.Message}"; }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_result is null || !CanSave) return;
        var dialog = new SaveFileDialog
        {
            Title = "保存标注图（同时保存二值图与测量 JSON）", Filter = "PNG 图片|*.png",
            FileName = "检测结果.png", DefaultExt = ".png", AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;
        IsBusy = true;
        var result = _result;
        var name = FileName;
        try
        {
            await Task.Run(() => ResultExporter.Export(result, dialog.FileName, name));
            Status = "已保存标注 PNG、二值 PNG 与测量 JSON。";
        }
        catch (Exception exception) { Status = $"保存失败：{exception.Message}"; }
        finally { IsBusy = false; }
    }

    private ProcessingParameters SnapshotParameters() => new(Threshold, MinimumArea, Invert, Blur);
    private void ParametersChanged()
    {
        Notify(nameof(CanSave));
        Notify(nameof(ResultState));
        RefreshCommands();
    }
    private void RefreshCommands()
    {
        foreach (var command in new[] { LoadSampleCommand, OpenCommand, ProcessCommand, SaveCommand, CancelCommand })
            ((AsyncCommand)command).Refresh();
    }
    public void RequestCancellation() => _cancellation?.Cancel();
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
