using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RecuperationSystem.Desktop.ViewModels.Cards;
using Serilog;

namespace RecuperationSystem.Desktop.Views.Controls;

public partial class FlowSpeedCard : UserControl
{
    private bool _isDragging = false;
    private bool _isCompletingDrag = false;
    private bool _ignoreValueChanged = false;
    private bool _initialized = false;
    private double _lastUserValue = 50;
    private double _dragStartValue = 50;
    private FlowSpeedCardViewModel? _viewModel;
    private Thumb? _sliderThumb;

    public FlowSpeedCard()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        FlowSpeedSlider.Loaded += OnSliderLoaded;
    }

    private void OnSliderLoaded(object? sender, RoutedEventArgs e)
    {
        _sliderThumb = FlowSpeedSlider.GetVisualDescendants()
            .OfType<Thumb>()
            .FirstOrDefault();
            
        if (_sliderThumb != null)
        {
            _sliderThumb.DragStarted += OnThumbDragStarted;
            _sliderThumb.DragCompleted += OnThumbDragCompleted;
            Log.Information("Slider thumb found and drag events attached");
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                _sliderThumb = FlowSpeedSlider.GetVisualDescendants()
                    .OfType<Thumb>()
                    .FirstOrDefault();
                    
                if (_sliderThumb != null)
                {
                    _sliderThumb.DragStarted += OnThumbDragStarted;
                    _sliderThumb.DragCompleted += OnThumbDragCompleted;
                    Log.Information("Slider thumb found on retry and drag events attached");
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as FlowSpeedCardViewModel;
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            TryInitializeSlider();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowSpeedCardViewModel.FlowSpeed))
        {
            if (!_initialized)
            {
                TryInitializeSlider();
            }
            else if (!_isDragging && !_isCompletingDrag && _viewModel != null)
            {
                var vmValue = _viewModel.FlowSpeed;
                if (Math.Abs(FlowSpeedSlider.Value - vmValue) > 0.1)
                {
                    _ignoreValueChanged = true;
                    FlowSpeedSlider.Value = vmValue;
                    _lastUserValue = vmValue;
                    UpdateDisplay(vmValue);
                    _ignoreValueChanged = false;
                    Log.Debug("Slider updated from ViewModel: {Value}", vmValue);
                }
            }
        }
    }

    private void TryInitializeSlider()
    {
        if (_initialized || _viewModel == null)
        {
            return;
        }

        var flowSpeed = _viewModel.FlowSpeed;
        
        if (flowSpeed != 50 || flowSpeed > 0)
        {
            _ignoreValueChanged = true;
            FlowSpeedSlider.Value = flowSpeed;
            _lastUserValue = flowSpeed;
            _dragStartValue = flowSpeed;
            UpdateDisplay(flowSpeed);
            _ignoreValueChanged = false;
            _initialized = true;
            
            Log.Information("FlowSpeedCard initialized with value: {Value}", flowSpeed);
        }
    }

    private void UpdateDisplay(double value)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FlowSpeedValue.Text = ((int)value).ToString();
        });
    }

    private void OnFlowSpeedValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_ignoreValueChanged || _viewModel == null)
        {
            return;
        }

        if (Math.Abs(e.NewValue - e.OldValue) < 0.1)
        {
            return;
        }
       
        _lastUserValue = e.NewValue;
        UpdateDisplay(e.NewValue);
        
        // Update ViewModel
        _viewModel.FlowSpeed = (int)e.NewValue;
        
        if (!_isDragging)
        {
            // User clicked on track - trigger API immediately
            Log.Information("User clicked slider at {Value}", (int)e.NewValue);
            _ = _viewModel.UpdateFlowSpeedCommand.Execute();
        }
    }

    private void OnThumbDragStarted(object? sender, VectorEventArgs e)
    {
        _isDragging = true;
        _dragStartValue = FlowSpeedSlider.Value;
        if (_viewModel != null)
        {
            _viewModel.IsDragging = true;
        }
        Log.Information("Slider drag started at {Value}", (int)FlowSpeedSlider.Value);
    }

    private void OnThumbDragCompleted(object? sender, VectorEventArgs e)
    {
        if (_isDragging)
        {
            _isCompletingDrag = true;
            _isDragging = false;
            
            var finalValue = (int)FlowSpeedSlider.Value;
            
            Log.Information("Slider drag completed - start: {Start}, end: {End}", (int)_dragStartValue, finalValue);
            
            if (_viewModel != null)
            {
                _viewModel.IsDragging = false;
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                _isCompletingDrag = false;
            }, DispatcherPriority.Background);
        }
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // This handles click on track (not thumb)
        Log.Debug("Pointer pressed on slider");
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Log.Debug("Pointer released on slider");
    }
}
