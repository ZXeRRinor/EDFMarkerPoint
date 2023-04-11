using OxyPlot.Series;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EDFCSharp;
using Microsoft.Win32;

namespace EDFReadAndClick;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public static PlotViewModel? ViewModel = null;
    public static List<Marker>? Markers = null;
    private static EDFFile? CurrentEdfFile = null;
    private static string CurrentMarkerFilePath = "";

    public int DrawStart { get; set; }
    public int DrawEnd { get; set; }
    public int SignalDuration { get; private set; }
    public int ScrollAmountMouseWheel { get; set; }
    public int ScrollAmountButtons { get; set; }

    private double PlotScale = 1;
    private int OriginalDrawArea = 0;
    private int OriginalScrollAmountMouseWheel = 0;
    private int OriginalScrollAmountButtons = 0;

    public MainWindow()
    {
        DrawStart = 0;
        DrawEnd = 1000;
        SignalDuration = 0;
        ScrollAmountMouseWheel = 100;
        ScrollAmountButtons = 400;
    }

    private void buttonOpenEdfFile_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "European Data Format document (*.edf)|*.edf|All files (*.*)|*.*";
        if (openFileDialog.ShowDialog() == true)
        {
            CurrentEdfFile = new EDFFile(openFileDialog.FileName);
            SignalDuration = (int)double.Ceiling(
                CurrentEdfFile.Header.EndTime.Subtract(CurrentEdfFile.Header.StartTime).TotalMilliseconds
            );
            textBoxSignalDuration.Text = SignalDuration.ToString();
            Markers = new List<Marker>();
            cbSignal.Items.Clear();
            foreach (var s in CurrentEdfFile.Signals)
            {
                cbSignal.Items.Add(s.Label.Value);
            }

            Title = "EDF Marker Point" + $" - [{openFileDialog.FileName}]";
        }
    }

    private void UpdatePlot()
    {
        int selectedIndex = cbSignal.SelectedIndex;
        if (selectedIndex == -1 || CurrentEdfFile == null || DrawStart < 0 || DrawEnd < 0 ||
            DrawStart > SignalDuration || DrawEnd > SignalDuration) return;
        ViewModel?.DrawPlot(CurrentEdfFile.Signals[selectedIndex].Label.Value, CurrentEdfFile.Signals[selectedIndex].Samples, SignalDuration, DrawStart, DrawEnd);
    }

    private void UpdatePlotAndValues()
    {
        textScrollMouseWheel.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        textScrollButtons.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        textFragmentStart.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        textFragmentEnd.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        UpdatePlot();
    }

    private void ScaleTo(double value)
    {
        if (value is >= 0.1 and <= 1000)
        {
            int centralPoint = (DrawEnd - DrawStart) / 2 + DrawStart;
            if (OriginalDrawArea == 0) OriginalDrawArea = DrawEnd - DrawStart;
            if (OriginalScrollAmountMouseWheel == 0) OriginalScrollAmountMouseWheel = ScrollAmountMouseWheel;
            if (OriginalScrollAmountButtons == 0) OriginalScrollAmountButtons = ScrollAmountButtons;
            PlotScale = value;
            ScrollAmountMouseWheel = (int)(OriginalScrollAmountMouseWheel * PlotScale);
            ScrollAmountButtons = (int)(OriginalScrollAmountButtons * PlotScale);
            int preDrawStart = (int)(centralPoint - OriginalDrawArea * PlotScale / 2);
            int preDrawEnd = (int)(centralPoint + OriginalDrawArea * PlotScale / 2);
            if (preDrawStart >= 0 && preDrawEnd < SignalDuration)
            {
                DrawStart = preDrawStart;
                DrawEnd = preDrawEnd;
            }
            else if (preDrawStart < 0 && preDrawEnd < SignalDuration && (preDrawEnd - preDrawStart) < SignalDuration)
            {
                DrawStart = 0;
                DrawEnd = preDrawEnd - preDrawStart;
            }
            else if (preDrawStart >= 0 && preDrawEnd >= SignalDuration &&
                     (preDrawStart - (preDrawEnd - SignalDuration + 1)) >= 0)
            {
                DrawStart = preDrawStart - (preDrawEnd - SignalDuration + 1);
                DrawEnd = SignalDuration - 1;
            }

            string scaleValuePercentageString = (100D / PlotScale).ToString(CultureInfo.InvariantCulture);
            textScaleValue.Text = scaleValuePercentageString.Substring(
                0, scaleValuePercentageString.Length >= 8 ? 8 : scaleValuePercentageString.Length
            );
            UpdatePlotAndValues();
        } else if (value < 0.1) ScaleTo(0.1);
        else if (value > 1000) ScaleTo(1000);
    }

    private void ScaleFor(double value)
    {
        ScaleTo(PlotScale + value);
    }

    private void ScrollFor(int value)
    {
        if (
            DrawStart + value >= 0 &&
            DrawEnd + value < SignalDuration
        )
        {
            DrawStart += value;
            DrawEnd += value;
            UpdatePlotAndValues();
        }
        else if (DrawStart + value < 0)
        {
            ScrollToStart();
        }
        else if (DrawEnd + value >= SignalDuration)
        {
            ScrollToEnd();
        }
    }

    private void ScrollToStart()
    {
        DrawEnd -= DrawStart;
        DrawStart = 0;
        UpdatePlotAndValues();
    }

    private void ScrollToEnd()
    {
        DrawStart = SignalDuration - (DrawEnd - DrawStart) - 1;
        DrawEnd = SignalDuration - 1;
        UpdatePlotAndValues();
    }

    private void RequestMarkerFilePath()
    {
        SaveFileDialog saveFileDialog = new SaveFileDialog();
        saveFileDialog.Filter = "Файл меток (*.mrk)|*.mrk";
        saveFileDialog.FilterIndex = 2;
        saveFileDialog.RestoreDirectory = true;

        if (saveFileDialog.ShowDialog() == true)
        {
            CurrentMarkerFilePath = saveFileDialog.FileName;
        }
    }

    private void SaveMarkerFile()
    {
        if (CurrentMarkerFilePath == "") RequestMarkerFilePath();
        File.Delete(CurrentMarkerFilePath);
        File.AppendAllTextAsync(CurrentMarkerFilePath, JsonSerializer.Serialize(Markers));
    }

    private void LoadMarkerFile()
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "Файл меток (*.mrk)|*.mrk|All files (*.*)|*.*";
        if (openFileDialog.ShowDialog() == true)
        {
            CurrentMarkerFilePath = openFileDialog.FileName;
            Markers = JsonSerializer.Deserialize<List<Marker>>(File.ReadAllText(CurrentMarkerFilePath));
            UpdatePlot();
            int selectedIndex = cbSignal.SelectedIndex;
            if (selectedIndex == -1 || CurrentEdfFile == null || DrawStart < 0 || DrawEnd < 0 ||
                DrawStart > SignalDuration || DrawEnd > SignalDuration) return;
            ViewModel?.DrawMarkersForSignal(CurrentEdfFile.Signals[selectedIndex].Label.Value);
        }
    }

    private void Plot_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            ScaleFor(-Math.Sign(e.Delta) * 0.1);
        else
            ScrollFor(-Math.Sign(e.Delta) * ScrollAmountMouseWheel);
    }

    private void cbSignal_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePlot();
    }

    private void textFragmentEnd_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdatePlotAndValues();
    }

    private void textFragmentStart_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdatePlotAndValues();
    }

    private void btnResetScale_Click(object sender, RoutedEventArgs e)
    {
        ScaleTo(1);
    }

    private void btnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            ScaleFor(0.1);
        else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            ScaleFor(10);
        else
            ScaleFor(1);
    }

    private void btnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            ScaleFor(-0.1);
        else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            ScaleFor(-10);
        else
            ScaleFor(-1);
    }

    private void btnScrollToStart_Click(object sender, RoutedEventArgs e)
    {
        ScrollToStart();
    }

    private void btnScrollBackward_Click(object sender, RoutedEventArgs e)
    {
        ScrollFor(-ScrollAmountButtons);
    }

    private void btnScrollForward_Click(object sender, RoutedEventArgs e)
    {
        ScrollFor(ScrollAmountButtons);
    }

    private void btnScrollToEnd_Click(object sender, RoutedEventArgs e)
    {
        ScrollToEnd();
    }

    private void buttonOpenMarkerFile_Click(object sender, RoutedEventArgs e)
    {
        LoadMarkerFile();
    }

    private void buttonSaveMarkerFile_Click(object sender, RoutedEventArgs e)
    {
        SaveMarkerFile();
    }

    private void buttonSaveMarkerFileAs_Click(object sender, RoutedEventArgs e)
    {
        RequestMarkerFilePath();
        SaveMarkerFile();
    }

    private void buttonHelp_Click(object sender, RoutedEventArgs e)
    {
        (new HelpWindow()).ShowDialog();
    }

    private void buttonAbout_Click(object sender, RoutedEventArgs e)
    {

    }
}