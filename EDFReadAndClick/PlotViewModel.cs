using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace EDFReadAndClick;

public class PlotViewModel
{
    public PlotModel MyModel { get; set; }
    public PlotController Controller { get; private set; }

    private int amountOfSamples = 1;
    private int durationMillis = 1;
    private string signalName = "";

    public PlotViewModel()
    {
        MainWindow.ViewModel = this;
        Controller = new OxyPlot.PlotController();
        Controller.Bind(new OxyPlot.OxyMouseEnterGesture(), OxyPlot.PlotCommands.HoverSnapTrack);
        this.MyModel = new PlotModel();
        MyModel.Axes.Add(new LinearAxis() { IsPanEnabled = false, IsZoomEnabled = false });
        MyModel.Axes.Add(new LinearAxis()
            { IsPanEnabled = false, IsZoomEnabled = false, Position = AxisPosition.Bottom });
    }

    private void DrawMarker(double x)
    {
        var annotation = new LineAnnotation
        {
            Color = OxyColors.Blue,
            MinimumY = -20000,
            MaximumY = 20000,
            X = x,
            LineStyle = LineStyle.Solid,
            Type = LineAnnotationType.Vertical
        };
        MyModel.Annotations.Add(annotation);
        MyModel.PlotView.InvalidatePlot();
    }

    private void SetGraphMouseBehavior(LineSeries lineSeries)
    {
        lineSeries.MouseDown += (s, e) =>
        {
            double x = (s as LineSeries).InverseTransform(e.Position).X;
            double y = (s as LineSeries).InverseTransform(e.Position).Y;
            DrawMarker(x);
            if (signalName != "")
                MainWindow.Markers?.Add(new Marker(signalName, (int)Math.Round(x), (int)Math.Round(y)));
            Debug.WriteLine($"X = {x}, Y = {y}");
        };
    }

    public void DrawMarkersForSignal(string name)
    {
        MyModel.Annotations.Clear();
        if (MainWindow.Markers?.Count > 0)
            foreach (Marker marker in MainWindow.Markers)
                if (marker.SignalName == name)
                    DrawMarker(marker.TimeMillis);
    }

    public void DrawPlot(string title, List<short> samples, double duration, double fromMillis, double toMillis)
    {
        MyModel.Series.Clear();
        var lineSeries = new LineSeries();
        amountOfSamples = samples.Count;
        int startIndex = (int)(amountOfSamples * (fromMillis / duration));
        int endIndex = (int)(amountOfSamples * (toMillis / duration));
        for (int index = startIndex; index < endIndex; index++)
            lineSeries.Points.Add(
                new DataPoint(((double)index / amountOfSamples) * duration, samples[index])
            );

        MyModel.Series.Add(lineSeries);
        if (title != signalName) DrawMarkersForSignal(title);

        signalName = title;
        MyModel.PlotView.InvalidatePlot();
        SetGraphMouseBehavior(lineSeries);
    }
}