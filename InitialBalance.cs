#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Custom.Indicators.Gemify.Shared;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Gemify
{
    [Gui.CategoryOrder("Initial Balance Period", 1)]
    [Gui.CategoryOrder("Display Mode", 2)]
    [Gui.CategoryOrder("Full Mode Options", 3)]
    [Gui.CategoryOrder("Minimal Mode Options", 4)]
    [Gui.CategoryOrder("Options", 5)]
    [Gui.CategoryOrder("IB Extention 1", 6)]
    [Gui.CategoryOrder("IB Extention 2", 7)]
    [Gui.CategoryOrder("IB Extention 3", 8)]
    [Gui.CategoryOrder("Colors", 9)]
    [Gui.CategoryOrder("Margin Marker Colors", 10)]
    public class InitialBalance : Indicator
    {
        private DateTime NTStartTime;

        private double _ibHigh;
        private double _ibLow;

        private double IBHighState;
        private double IBLowState;

        private int IBStartBar;

        private bool _ibComplete;

        private double _ibx1Upper;
        private double _ibx1Lower;
        private double _ibx2Upper;
        private double _ibx2Lower;
        private double _ibx3Upper;
        private double _ibx3Lower;

        private List<double> Ranges;

        private double _orHigh;
        private double _orLow;
        private bool _orComplete;

        private double _sessionHigh;
        private double _sessionLow;
        private double _sessionMiddle;

        private DateTime dtCurrentIBStart;
        private DateTime dtHistoricalIBStart;

        private bool IsDebug;

        public enum IBDisplayModeEnum
        {
            FULL,
            MINIMAL
        }

        public InitialBalance()
        {
        }

        protected override void OnStateChange()
        {
            Debug(this.Name + ":>>> " + State);

            if (State == State.SetDefaults)
            {
                Description = @"Displays Initial Balance and Extensions";
                Name = "\"Initial Balance\"";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                //Disable this property if your indicator requires custom values that cumulate with each new market data event. 
                //See Help Guide for additional information.
                IsSuspendedWhileInactive = true;
                PaintPriceMarkers = true;

                // Defaults

                NTStartTime = DateTime.MinValue;

                IsDebug = false;

                DisplayMode = IBDisplayModeEnum.FULL;
                MarkersOnlyIBRangeTextPosition = TextPosition.TopRight;

                PlotHistoricalIBs = true;
                HistoricalIBLookback = 15;

                DisplayOR = false;
                _orComplete = false;
                ORLineColor = Brushes.Gray;

                _sessionHigh = 0;
                _sessionLow = 0;
                DisplaySessionMid = true;
                SessionMidColor = Brushes.Yellow;

                DisplayIBRange = true;
                HighlightIBPeriod = true;
                HighlightSessionPeriod = true;
                DisplayMarginMarkers = true;

                IBStartBar = -1;
                _ibComplete = false;

                // Default Initial Balance set between 9:30 to 10:30 AM EST
                IBStartTime = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
                IBEndTime = DateTime.Parse("10:30", System.Globalization.CultureInfo.InvariantCulture);
                SessionEndTime = DateTime.Parse("16:15", System.Globalization.CultureInfo.InvariantCulture);

                IBHighState = 0;
                IBLowState = 0;

                IBFillColor = Brushes.Blue;
                IBFillOpacity = 10;

                IBHighlightColor = Brushes.Yellow;
                IBHighlightOpacity = 2;

                TextFont = new SimpleFont("Verdana", 11);
                TextColor = Brushes.HotPink;

                // IB Extension defaults
                DisplayIBX1 = true;
                IBX1Multiple = 1.5;
                IBX1Color = Brushes.DarkOrange;
                IBX1DashStyle = DashStyleHelper.Dash;
                IBX1Width = 1;

                DisplayIBX2 = true;
                IBX2Multiple = 2.0;
                IBX2Color = Brushes.DarkOrchid;
                IBX2DashStyle = DashStyleHelper.Dash;
                IBX2Width = 1;

                DisplayIBX3 = true;
                IBX3Multiple = 3.0;
                IBX3Color = Brushes.DarkSalmon;
                IBX3DashStyle = DashStyleHelper.Dash;
                IBX3Width = 1;

                MarginMarkerTextBrush = Brushes.White;
                MarginMarkerFillBrush = Brushes.Maroon;
                MarginMarkerBorderBrush = Brushes.AliceBlue;
            }
            else if (State == State.Configure)
            {
                Debug("Adding Seconds Data Series");
                if (DisplayOR)
                {
                    // Add seconds based bars to determine Initial Balance.
                    // Note: Accuracy of the UI/plotting is based on granularity of timestamps.
                    // A single-second series will be highly accurate on the UI, but CPU expensive.

                    // The values that matter - IBH, IBL, etc will be accurate regardless.
                    AddDataSeries(Instrument.FullName, new BarsPeriod() { BarsPeriodType=BarsPeriodType.Second, Value=30 }, "CME US Index Futures RTH", true);
                }
                else
                {
                    AddDataSeries(BarsPeriodType.Minute, 1);
                }

                // No IBs to be drawn on UI if in Minimal mode
                PlotHistoricalIBs = DisplayMode == IBDisplayModeEnum.MINIMAL ? false : PlotHistoricalIBs;
                // Markers are mandatory in Minimal mode :)
                DisplayMarginMarkers = DisplayMode == IBDisplayModeEnum.MINIMAL ? true : DisplayMarginMarkers;

                AddPlot(Brushes.Transparent, "IBHPlot");
                AddPlot(Brushes.Transparent, "SessMidPlot");
                AddPlot(Brushes.Transparent, "IBLPlot");

                AddPlot(Brushes.Transparent, "ORHPlot");
                AddPlot(Brushes.Transparent, "ORLPlot");

                Ranges = new List<double>();
            }
            else if (State == State.DataLoaded)
            {
                dtCurrentIBStart = BarsArray[1].LastBarTime.Date;
                Debug("Setting current IB datetime to " + dtCurrentIBStart);

                dtHistoricalIBStart = SubtractWeekdays(dtCurrentIBStart, HistoricalIBLookback);
                Debug("Setting historical IB limit (lookback) datetime to " + dtHistoricalIBStart);
            }
        }

        private DateTime SubtractWeekdays(DateTime startDate, int subtractDays)
        {
            for (int i = 0; i < subtractDays; i++)
            {
                do{ startDate = startDate.AddDays(-1); } 
                while (startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday);
            }
            return startDate;
        }

        private void Debug(String message)
        {
            if (IsDebug) Print(message);
        }

        protected override void OnBarUpdate()
        {
            // Work only with the second bar series
            if (BarsInProgress != 1)
            {
                return;
            }

            // Clear out stuff if first bar of session
            if (Bars.IsFirstBarOfSession)
            {
                Reset();
            }

            // Get current time and use that to calculate today's OR Start/End times
            DateTime now = Times[1][0]; // Times[1] based time - could be seconds or minutes (depending on whether OpeningRange is enabled)

            if (!PlotHistoricalIBs && now < dtCurrentIBStart)
            {
                Debug("Current IB only. Skipping historical bar " + now + ". Waiting for " + dtCurrentIBStart);
                return;
            }

            if (PlotHistoricalIBs && now < dtHistoricalIBStart)
            {
                Debug("Skipping historical bar " + now + ". Waiting for " + dtHistoricalIBStart);
                return;
            }

            DateTime StartTime = new DateTime(now.Year, now.Month, now.Day, IBStartTime.Hour, IBStartTime.Minute, IBStartTime.Second, DateTimeKind.Local);
            DateTime EndTime = new DateTime(now.Year, now.Month, now.Day, IBEndTime.Hour, IBEndTime.Minute, IBEndTime.Second, DateTimeKind.Local);
            // Add 30 seconds to the start time
            DateTime OREndTime = StartTime.AddSeconds(30);

            // Calculate session end time 
            DateTime TodaySessionEndTime = new DateTime(now.Year, now.Month, now.Day, SessionEndTime.Hour, SessionEndTime.Minute, SessionEndTime.Second, DateTimeKind.Local);

            // Nothing to do if we're outside of working hours.
            if (!(StartTime < now && now <= TodaySessionEndTime)) return;

            // Once we're inside the working hours, record the starting bar's timestamp
            // This will be used for UI/drawing purposes only
            if (NTStartTime == DateTime.MinValue)
            {
                NTStartTime = Times[1][0];
            }

            // Session mid value
            _sessionHigh = Math.Max(_sessionHigh, High[0]);
            _sessionLow = Math.Min(_sessionLow, Low[0]);
            _sessionMiddle = Instrument.MasterInstrument.RoundToTickSize((_sessionHigh + _sessionLow) / 2.0);

            Debug("SessionMid is " + _sessionMiddle);

            // Flag that indicates that IB is complete.
            //    - We have an IB start bar, and
            //    - we're past the IB end time
            _ibComplete = IBStartBar != -1 && now > EndTime;

            // Calculate highest and lowest prices if current time is between Start and End
            if (!_ibComplete && StartTime < now && now <= EndTime)
            {
                // Keep track of the bar# (of the 1-minute series)
                // when the IB started. We'll use this later to
                // display the range text.
                IBStartBar = IBStartBar == -1 ? CurrentBars[0] : IBStartBar;

                // Calculate the Highest and Lowest prices of the IB
                _ibHigh = Math.Max(_ibHigh, High[0]);
                _ibLow = Math.Min(_ibLow, Low[0]);

                Debug("Time: " + now + ". ORH: " + _ibHigh + ", ORL: " + _ibLow);
            }

            _orComplete = IBStartBar != -1 && now > OREndTime;

            // If OR calculation is NOT complete and we're still within the OR timeframe
            if (!_orComplete && StartTime < now && now <= EndTime)
            {
                Debug("Calculating Opening Range.");

                // Calculate the Highest and Lowest prices of the OR
                _orHigh = Math.Max(_orHigh, High[0]);
                _orLow = Math.Min(_orLow, Low[0]);
                //Values[3][0] = ORH;
                //Values[4][0] = ORL;

                Debug("Time: " + now + ". ORH: " + _orHigh + ", ORL: " + _orLow);
            }

            String tag = GenerateTodayTag(now);

            // Draw the IB Rectangle if:
            //    - we have an Initial Balance, and
            //    - the session is still open, and
            //	  - the IB high/low has changed (compare prev state - prevents unnecessary drawing cycles)
            if (_ibHigh > double.MinValue && _ibLow < double.MaxValue &&
                now <= TodaySessionEndTime &&
                (IBHighState != _ibHigh || IBLowState != _ibLow))
            {

                if (DisplayMode == IBDisplayModeEnum.FULL)
                {
                    SetZOrder(-1);

                    if (HighlightSessionPeriod)
                    {
                        // Draw the IB range for the entire session
                        Draw.Rectangle(this, "gemifyIB_IB_Session" + tag, false, NTStartTime, _ibHigh, TodaySessionEndTime, _ibLow, IBFillColor, IBFillColor, IBFillOpacity);
                    }

                    // Highlight IB Period if desired
                    if (HighlightIBPeriod)
                    {
                        Draw.Rectangle(this, "gemifyIB_IB" + tag, false, NTStartTime, _ibHigh, EndTime, _ibLow, IBHighlightColor, IBHighlightColor, IBHighlightOpacity);
                    }
                }

                // Save IB high/low state for comparing during the next update
                IBHighState = _ibHigh;
                IBLowState = _ibLow;
            }


            // Calculate and display IB range if desired
            if (DisplayIBRange)
            {
                double range = Instrument.MasterInstrument.RoundToTickSize(_ibHigh - _ibLow);
                Debug("Time: " + now + ", Range is : " + range);

                // Keep track of ranges
                if (_ibComplete) Ranges.Add(range);

                Debug("Calculating range");
                // Find the current median range
                double medianRange = CalculateMedian(Ranges);
                Debug("Median range: " + medianRange);

                if (DisplayMode == IBDisplayModeEnum.FULL)
                {
                    double y = _ibHigh + (2 * Instrument.MasterInstrument.TickSize);
                    SetZOrder(int.MaxValue);

                    if (!_ibComplete)
                    {
                        Debug("Developing IB...");
                        TimeSpan IBDuration = EndTime - StartTime;
                        TimeSpan Elapsed = now - StartTime;
                        double Progress = (double)Elapsed.Ticks / (double)IBDuration.Ticks;
                        Debug("Percentage complete: " + Progress);
                        Draw.Text(this, "gemifyIB_IBR" + tag, false, "IB Forming (" + Progress.ToString("P", CultureInfo.InvariantCulture) + "). Current Range: " + range.ToString() + ", Median: " + String.Format("{0:0.0#}", medianRange), NTStartTime, y, 10, TextColor, TextFont, TextAlignment.Left, null, null, 100);
                        Debug("Done drawing progress text.");
                    }
                    else
                    {
                        Debug("Time: " + now + ", Range is : " + range);
                        Debug("IB Complete...");
                        Draw.Text(this, "gemifyIB_IBR" + tag, false, "IB Range: " + range.ToString() + ", Median: " + String.Format("{0:0.0#}", medianRange), NTStartTime, y, 10, TextColor, TextFont, TextAlignment.Left, null, null, 100);
                        Debug("Done drawing range text.");
                    }


                    if (DisplayOR && _orComplete)
                    {
                        Draw.Text(this, "gemifyIB_ORHText" + tag, false, "ORH", NTStartTime, (_orHigh + Instrument.MasterInstrument.TickSize), 10, ORLineColor, TextFont, TextAlignment.Left, null, null, 100);
                        Draw.Line(this, "gemifyIB_ORHigh" + tag, false, NTStartTime, _orHigh, TodaySessionEndTime, _orHigh, ORLineColor, DashStyleHelper.DashDot, 1);
                        Draw.Line(this, "gemifyIB_ORLow" + tag, false, NTStartTime, _orLow, TodaySessionEndTime, _orLow, ORLineColor, DashStyleHelper.DashDot, 1);
                        Draw.Text(this, "gemifyIB_ORLText" + tag, false, "ORL", NTStartTime, (_orLow - Instrument.MasterInstrument.TickSize), -10, ORLineColor, TextFont, TextAlignment.Left, null, null, 100);
                    }

                    if (DisplaySessionMid)
                    {
                        // Draw Session Mid
                        Draw.Text(this, "gemifyIB_SessionMidText", false, "Session Mid", NTStartTime, (_sessionMiddle + Instrument.MasterInstrument.TickSize), 10, SessionMidColor, TextFont, TextAlignment.Left, null, null, 100);
                        Draw.Line(this, "gemifyIB_SessionMid", false, NTStartTime, _sessionMiddle, TodaySessionEndTime, _sessionMiddle, SessionMidColor, DashStyleHelper.DashDotDot, 2);
                    }
                }
                else // Minimal mode
                {
                    if (!_ibComplete)
                    {
                        Debug("Developing IB...");
                        TimeSpan IBDuration = EndTime - StartTime;
                        TimeSpan Elapsed = now - StartTime;
                        double Progress = (double)Elapsed.Ticks / (double)IBDuration.Ticks;
                        Debug("Percentage complete: " + Progress);
                        Draw.TextFixed(this, "gemifyIB_IBR" + tag, "IB Forming (" + Progress.ToString("P", CultureInfo.InvariantCulture) + "). Current Range: " + range.ToString(), MarkersOnlyIBRangeTextPosition, MarginMarkerTextBrush, TextFont, MarginMarkerBorderBrush, MarginMarkerFillBrush, 70);
                        Debug("Done drawing progress text.");
                    }
                    else
                    {
                        Debug("Time: " + now + ", Range is : " + range);
                        Debug("IB Complete...");
                        Draw.TextFixed(this, "gemifyIB_IBR" + tag, "IB Range: " + range.ToString(), MarkersOnlyIBRangeTextPosition, MarginMarkerTextBrush, TextFont, MarginMarkerBorderBrush, MarginMarkerFillBrush, 70);
                        Debug("Done drawing range text.");
                    }
                }
            }


            // Draw Extensions if IB is complete
            if (_ibComplete)
            {
                // Calculate range for extensions
                double range = Instrument.MasterInstrument.RoundToTickSize(_ibHigh - _ibLow);

                int barsAgo = (CurrentBars[0] - IBStartBar);

                double offset = range * (IBX1Multiple - 1.0);
                Debug("IBX1 - Range : " + range + ", offset : " + offset);

                _ibx1Upper = _ibHigh + offset;
                _ibx1Lower = _ibLow - offset;

                offset = range * (IBX2Multiple - 1.0);
                Debug("IBX2 - Range : " + range + ", offset : " + offset);

                _ibx2Upper = _ibHigh + offset;
                _ibx2Lower = _ibLow - offset;

                offset = range * (IBX3Multiple - 1.0);
                Debug("IBX3 - Range : " + range + ", offset : " + offset);

                _ibx3Upper = _ibHigh + offset;
                _ibx3Lower = _ibLow - offset;

                if (DisplayMode == IBDisplayModeEnum.FULL)
                {
                    if (DisplayIBX1)
                    {
                        Draw.Line(this, "gemifyIB_IBX1Up" + tag, false, NTStartTime, _ibx1Upper, TodaySessionEndTime, _ibx1Upper, IBX1Color, IBX1DashStyle, IBX1Width);
                        Draw.Line(this, "gemifyIB_IBX1Down" + tag, false, NTStartTime, _ibx1Lower, TodaySessionEndTime, _ibx1Lower, IBX1Color, IBX1DashStyle, IBX1Width);

                        String multiple = String.Format("{0:0.0#}", IBX1Multiple);
                        Draw.Text(this, "gemifyIB_IBX1UpText" + tag, false, "IBx" + multiple, NTStartTime, (_ibx1Upper + Instrument.MasterInstrument.TickSize), 10, IBX1Color, TextFont, TextAlignment.Left, null, null, 100);
                        Draw.Text(this, "gemifyIB_IBX1DownText" + tag, false, "IBx" + multiple, NTStartTime, (_ibx1Lower + Instrument.MasterInstrument.TickSize), 10, IBX1Color, TextFont, TextAlignment.Left, null, null, 100);
                    }

                    if (DisplayIBX2)
                    {
                        Draw.Line(this, "gemifyIB_IBX2Up" + tag, false, NTStartTime, _ibx2Upper, TodaySessionEndTime, _ibx2Upper, IBX2Color, IBX2DashStyle, IBX2Width);
                        Draw.Line(this, "gemifyIB_IBX2Down" + tag, false, NTStartTime, _ibx2Lower, TodaySessionEndTime, _ibx2Lower, IBX2Color, IBX2DashStyle, IBX2Width);

                        String multiple = String.Format("{0:0.0#}", IBX2Multiple);
                        Draw.Text(this, "gemifyIB_IBX2UpText" + tag, false, "IBx" + multiple, NTStartTime, (_ibx2Upper + Instrument.MasterInstrument.TickSize), 10, IBX2Color, TextFont, TextAlignment.Left, null, null, 100);
                        Draw.Text(this, "gemifyIB_IBX2DownText" + tag, false, "IBx" + multiple, NTStartTime, (_ibx2Lower + Instrument.MasterInstrument.TickSize), 10, IBX2Color, TextFont, TextAlignment.Left, null, null, 100);

                    }

                    if (DisplayIBX3)
                    {
                        Draw.Line(this, "gemifyIB_IB31Up" + tag, false, NTStartTime, _ibx3Upper, TodaySessionEndTime, _ibx3Upper, IBX3Color, IBX3DashStyle, IBX3Width);
                        Draw.Line(this, "gemifyIB_IB31Down" + tag, false, NTStartTime, _ibx3Lower, TodaySessionEndTime, _ibx3Lower, IBX3Color, IBX3DashStyle, IBX3Width);

                        String multiple = String.Format("{0:0.0#}", IBX3Multiple);
                        Draw.Text(this, "gemifyIB_IBX3UpText" + tag, false, "IBx" + multiple, NTStartTime, (_ibx3Upper + Instrument.MasterInstrument.TickSize), 10, IBX3Color, TextFont, TextAlignment.Left, null, null, 100);
                        Draw.Text(this, "gemifyIB_IBX3DownText" + tag, false, "IBx" + multiple, NTStartTime, (_ibx3Lower + Instrument.MasterInstrument.TickSize), 10, IBX3Color, TextFont, TextAlignment.Left, null, null, 100);
                    }
                }
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            // Operates only on the secondary time series (minute)
            if (State == State.Historical || BarsInProgress != 1 || !DisplayMarginMarkers) return;

            DrawText(chartScale, (_ibComplete ? "" : "...") + "IBH: " + _ibHigh, _ibHigh);
            DrawText(chartScale, (_ibComplete ? "" : "...") + "IBL: " + _ibLow, _ibLow);
            DrawText(chartScale, "SessMid: " + _sessionMiddle, _sessionMiddle);
            if (DisplayOR && _orComplete)
            {
                DrawText(chartScale, "ORH: " + _orHigh, _orHigh);
                DrawText(chartScale, "ORL: " + _orLow, _orLow);
            }
            if (_ibComplete)
            {
                if (DisplayIBX1)
                {
                    DrawIBXMarker(chartScale, IBX1Multiple, _ibx1Upper, _ibx1Lower);
                }
                if (DisplayIBX2)
                {
                    DrawIBXMarker(chartScale, IBX2Multiple, _ibx2Upper, _ibx2Lower);
                }
                if (DisplayIBX3)
                {
                    DrawIBXMarker(chartScale, IBX3Multiple, _ibx3Upper, _ibx3Lower);
                }
            }
        }

        private void DrawIBXMarker(ChartScale chartScale, double ibxMultiple, double upper, double lower)
        {
            String multiple = String.Format("{0:0.0#}", ibxMultiple);
            DrawText(chartScale, "IBx" + multiple + ": " + upper, upper);
            DrawText(chartScale, "IBx" + multiple + ": " + lower, lower);
        }

        private void DrawText(ChartScale chartScale, String TextToWrite, double Price)
        {
            using (SharpDX.Direct2D1.Brush TextBrushDx = MarginMarkerTextBrush.ToDxBrush(RenderTarget))
            using (SharpDX.Direct2D1.Brush FillBrushDx = MarginMarkerFillBrush.ToDxBrush(RenderTarget))
            using (SharpDX.Direct2D1.Brush DevelopingIBBrushDx = Brushes.DimGray.ToDxBrush(RenderTarget))
            using (SharpDX.Direct2D1.Brush BorderBrushDx = MarginMarkerBorderBrush.ToDxBrush(RenderTarget))
            {
                SharpDX.DirectWrite.TextFormat textFormat = TextFont.ToDirectWriteTextFormat();
                SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, TextToWrite, textFormat, ChartPanel.X + ChartPanel.W, textFormat.FontSize);
                SharpDX.DirectWrite.TextLayout markerTextLayout = new SharpDX.DirectWrite.TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, ">", textFormat, ChartPanel.X + ChartPanel.W, textFormat.FontSize);

                float textWidth = textLayout.Metrics.Width;
                float textHeight = textLayout.Metrics.Height;

                float x = (float)(ChartPanel.W - textWidth - (8 + 5));
                int priceCoordinate = chartScale.GetYByValue(Price);
                float rectY = priceCoordinate - ((textHeight + 7) / (float)2.0);
                float textY = priceCoordinate - (textHeight / (float)2.0);

                SharpDX.Vector2 startPoint = new SharpDX.Vector2(x, rectY);
                SharpDX.Vector2 upperTextPoint = new SharpDX.Vector2(startPoint.X + 4, textY);
                SharpDX.Vector2 markerPoint = new SharpDX.Vector2(ChartPanel.W - markerTextLayout.Metrics.Width + 2, textY);

                SharpDX.RectangleF rect = new SharpDX.RectangleF(startPoint.X, startPoint.Y, textWidth + 8, textHeight + 6);
                RenderTarget.FillRectangle(rect, _ibComplete ? FillBrushDx : DevelopingIBBrushDx);
                RenderTarget.DrawRectangle(rect, BorderBrushDx, 1);

                RenderTarget.DrawTextLayout(markerPoint, markerTextLayout, BorderBrushDx, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

                RenderTarget.DrawTextLayout(upperTextPoint, textLayout, TextBrushDx, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
            }
        }

        private void Reset()
        {
            Debug("First bar of session. Resetting OR values.");

            // Reset values
            NTStartTime = DateTime.MinValue;

            _ibHigh = double.MinValue;
            _ibLow = double.MaxValue;
            IBHighState = 0;
            IBLowState = 0;
            IBStartBar = -1;
            _ibComplete = false;

            _orComplete = false;
            _orHigh = double.MinValue;
            _orLow = double.MaxValue;

            _sessionHigh = double.MinValue;
            _sessionLow = double.MaxValue;
            _sessionMiddle = double.MinValue;

            if (!PlotHistoricalIBs)
            {
                RemoveDrawObjects();
            }
        }

        private String GenerateTodayTag(DateTime now)
        {
            return "_" + now.Month + now.Day + now.Year;
        }

        private double CalculateMedian(List<double> ranges)
        {
            if (ranges.IsNullOrEmpty()) return 0;

            double[] data = ranges.ToArray();
            Array.Sort(data);

            if (data.Length % 2 == 0)
                return (data[data.Length / 2 - 1] + data[data.Length / 2]) / 2;
            else
                return data[data.Length / 2];
        }

        #region Properties

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance Begin", Description = "Initial balance begin time", Order = 100, GroupName = "Initial Balance Period")]
        public DateTime IBStartTime
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance End", Description = "Initial balance end time", Order = 200, GroupName = "Initial Balance Period")]
        public DateTime IBEndTime
        { get; set; }

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Session End Time", Description = "Session end time", Order = 300, GroupName = "Initial Balance Period")]
        public DateTime SessionEndTime
        { get; set; }

        // ----------- Options
        [Display(Name = "Display Mode", Description = "Full or Minimal mode", Order = 100, GroupName = "Display Mode")]
        public IBDisplayModeEnum DisplayMode
        { get; set; }

        [Display(Name = "IB Range Text Position", Description = "IB Range Text Position", Order = 100, GroupName = "Minimal Mode Options")]
        public TextPosition MarkersOnlyIBRangeTextPosition
        { get; set; }


        [Display(Name = "Plot Historical IBs", Description = "Plot Historical IBs", Order = 50, GroupName = "Full Mode Options")]
        public bool PlotHistoricalIBs
        { get; set; }

        [Range(0, 365)]
        [Display(Name = "Max Historical IBs (days)", Description = "Number of Historical IBs to calculate (Max 365 days)", Order = 75, GroupName = "Full Mode Options")]
        public int HistoricalIBLookback
        { get; set; }

        [Display(Name = "Highlight IB Period", Description = "Highlight IB Period", Order = 100, GroupName = "Full Mode Options")]
        public bool HighlightIBPeriod
        { get; set; }

        [Display(Name = "Highlight Session", Description = "Highlight Session", Order = 150, GroupName = "Full Mode Options")]
        public bool HighlightSessionPeriod
        { get; set; }


        [Display(Name = "Display IB Range Text", Description = "Display IB Range Text", Order = 200, GroupName = "Options")]
        public bool DisplayIBRange
        { get; set; }

        [Display(Name = "Display Opening Range (30 sec)", Description = "Display Opening Range (30 seconds)", Order = 300, GroupName = "Options")]
        public bool DisplayOR
        { get; set; }

        [Display(Name = "Display Session Mid", Description = "Display Session Mid", Order = 400, GroupName = "Options")]
        public bool DisplaySessionMid
        { get; set; }

        [Display(Name = "Price Markers", Description = "Display Markers on the Right Margin", Order = 500, GroupName = "Options")]
        public bool DisplayMarginMarkers
        { get; set; }



        // ----------- IB Extensions

        [Display(Name = "Display IB Extension", Description = "Display IB Extension", Order = 100, GroupName = "IB Extension 1")]
        public bool DisplayIBX1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "IB Extension Multiple", Description = "IB Extension Multiple", Order = 200, GroupName = "IB Extension 1")]
        public double IBX1Multiple
        { get; set; }

        [XmlIgnore]
        [Display(Name = "IB Extension Line Color", Description = "IB Extension line color", Order = 300, GroupName = "IB Extension 1")]
        public Brush IBX1Color
        { get; set; }

        [Browsable(false)]
        public string IBX1ColorSerializable
        {
            get { return Serialize.BrushToString(IBX1Color); }
            set { IBX1Color = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "IB Extension Line DashStyle", Description = "IB Extension line dash style", Order = 400, GroupName = "IB Extension 1")]
        public DashStyleHelper IBX1DashStyle
        { get; set; }

        [XmlIgnore]
        [Range(1, 10)]
        [Display(Name = "IB Extension Line Thickness", Description = "IB Extension line thickness", Order = 500, GroupName = "IB Extension 1")]
        public int IBX1Width
        { get; set; }




        [Display(Name = "Display IB Extension", Description = "Display IB Extension", Order = 100, GroupName = "IB Extension 2")]
        public bool DisplayIBX2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "IB Extension Multiple", Description = "IB Extension Multiple", Order = 200, GroupName = "IB Extension 2")]
        public double IBX2Multiple
        { get; set; }

        [XmlIgnore]
        [Display(Name = "IB Extension Line Color", Description = "IB Extension line color", Order = 300, GroupName = "IB Extension 2")]
        public Brush IBX2Color
        { get; set; }

        [Browsable(false)]
        public string IBX2ColorSerializable
        {
            get { return Serialize.BrushToString(IBX2Color); }
            set { IBX2Color = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "IB Extension Line DashStyle", Description = "IB Extension line dash style", Order = 400, GroupName = "IB Extension 2")]
        public DashStyleHelper IBX2DashStyle
        { get; set; }

        [XmlIgnore]
        [Range(1, 10)]
        [Display(Name = " Extension Line Thickness", Description = "IB Extension line thickness", Order = 500, GroupName = "IB Extension 2")]
        public int IBX2Width
        { get; set; }



        [Display(Name = "Display IB Extension", Description = "Display IB Extension", Order = 100, GroupName = "IB Extension 3")]
        public bool DisplayIBX3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "IB Extension Multiple", Description = "IB Extension Multiple", Order = 200, GroupName = "IB Extension 3")]
        public double IBX3Multiple
        { get; set; }

        [XmlIgnore]
        [Display(Name = "IB Extension Line Color", Description = "IB Extension line color", Order = 300, GroupName = "IB Extension 3")]
        public Brush IBX3Color
        { get; set; }

        [Browsable(false)]
        public string IBX3ColorSerializable
        {
            get { return Serialize.BrushToString(IBX3Color); }
            set { IBX3Color = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "IB Extension Line DashStyle", Description = "IB Extension line dash style", Order = 400, GroupName = "IB Extension 3")]
        public DashStyleHelper IBX3DashStyle
        { get; set; }

        [XmlIgnore]
        [Range(1, 10)]
        [Display(Name = " Extension Line Thickness", Description = "IB Extension line thickness", Order = 500, GroupName = "IB Extension 3")]
        public int IBX3Width
        { get; set; }

        // ----------- Colors

        [XmlIgnore]
        [Display(Name = "Initial Balance Fill Color", Description = "Initial Balance fill color", Order = 100, GroupName = "Colors")]
        public Brush IBFillColor
        { get; set; }

        [Browsable(false)]
        public string IBFillColorSerializable
        {
            get { return Serialize.BrushToString(IBFillColor); }
            set { IBFillColor = Serialize.StringToBrush(value); }
        }

        [Range(1, 100)]
        [Display(Name = "Opacity", Description = "Opacity of Initial Balance", Order = 200, GroupName = "Colors")]
        public int IBFillOpacity
        { get; set; }

        [XmlIgnore]
        [Display(Name = "Initial Balance Hightlight Color", Description = "Initial Balance highlight color", Order = 300, GroupName = "Colors")]
        public Brush IBHighlightColor
        { get; set; }

        [Browsable(false)]
        public string IBHighlightColorSerializable
        {
            get { return Serialize.BrushToString(IBHighlightColor); }
            set { IBHighlightColor = Serialize.StringToBrush(value); }
        }

        [Range(1, 100)]
        [Display(Name = "IB Highlight Opacity", Description = "Initial Balance hightlight opacity", Order = 400, GroupName = "Colors")]
        public int IBHighlightOpacity
        { get; set; }

        [XmlIgnore]
        [Display(Name = "IB Range Text Color", Description = "Color to use to display IB range", Order = 500, GroupName = "Colors")]
        public Brush TextColor
        { get; set; }

        [Browsable(false)]
        public string TextColorSerializable
        {
            get { return Serialize.BrushToString(TextColor); }
            set { TextColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "IB Range Text Font", Description = "Font to use to display IB range", Order = 600, GroupName = "Colors")]
        public SimpleFont TextFont
        { get; set; }

        [XmlIgnore]
        [Display(Name = "Opening Range Lines Color", Description = "Opening Range lines color", Order = 700, GroupName = "Colors")]
        public Brush ORLineColor
        { get; set; }

        [Browsable(false)]
        public string ORLineColorSerializable
        {
            get { return Serialize.BrushToString(ORLineColor); }
            set { ORLineColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Session Mid Line Color", Description = "Session Mid line color", Order = 800, GroupName = "Colors")]
        public Brush SessionMidColor
        { get; set; }

        [Browsable(false)]
        public string SessionMidColorSerializable
        {
            get { return Serialize.BrushToString(SessionMidColor); }
            set { SessionMidColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Margin Marker Text Color", Description = "Margin Marker Text color", Order = 100, GroupName = "Margin Marker Colors")]
        public Brush MarginMarkerTextBrush
        { get; set; }

        [Browsable(false)]
        public string MarginMarkerTextBrushSerializable
        {
            get { return Serialize.BrushToString(MarginMarkerTextBrush); }
            set { MarginMarkerTextBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Margin Marker Fill Color", Description = "Margin Marker Fill color", Order = 200, GroupName = "Margin Marker Colors")]
        public Brush MarginMarkerFillBrush
        { get; set; }

        [Browsable(false)]
        public string MarginMarkerFillBrushSerializable
        {
            get { return Serialize.BrushToString(MarginMarkerFillBrush); }
            set { MarginMarkerFillBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Margin Marker Border Color", Description = "Margin Marker Border color", Order = 300, GroupName = "Margin Marker Colors")]
        public Brush MarginMarkerBorderBrush
        { get; set; }

        [Browsable(false)]
        public string MarginMarkerBorderBrushSerializable
        {
            get { return Serialize.BrushToString(MarginMarkerBorderBrush); }
            set { MarginMarkerBorderBrush = Serialize.StringToBrush(value); }
        }

        #endregion

        #region Public Properties

        [Browsable(false)]
        [XmlIgnore()]
        public bool IsIBComplete
        {
            get { return _ibComplete; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public bool IsORComplete
        {
            get { return _orComplete; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double IBHigh
        {
            get { return _ibHigh; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double IBLow
        {
            get { return this._ibLow; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double ORHigh
        {
            get { return _orHigh; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double ORLow
        {
            get { return _orLow; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double SessionMid
        {
            get { return _sessionMiddle; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double IBX1Upper
        {
            get { return _ibx1Upper; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public double IBX1Lower
        {
            get { return _ibx1Lower; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public double IBX2Upper
        {
            get { return _ibx2Upper; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public double IBX2Lower
        {
            get { return _ibx2Lower; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public double IBX3Upper
        {
            get { return _ibx3Upper; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public double IBX3Lower
        {
            get { return _ibx3Lower; }
        }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Gemify.InitialBalance[] cacheInitialBalance;
		public Gemify.InitialBalance InitialBalance(DateTime iBStartTime, DateTime iBEndTime, double iBX1Multiple, double iBX2Multiple, double iBX3Multiple)
		{
			return InitialBalance(Input, iBStartTime, iBEndTime, iBX1Multiple, iBX2Multiple, iBX3Multiple);
		}

		public Gemify.InitialBalance InitialBalance(ISeries<double> input, DateTime iBStartTime, DateTime iBEndTime, double iBX1Multiple, double iBX2Multiple, double iBX3Multiple)
		{
			if (cacheInitialBalance != null)
				for (int idx = 0; idx < cacheInitialBalance.Length; idx++)
					if (cacheInitialBalance[idx] != null && cacheInitialBalance[idx].IBStartTime == iBStartTime && cacheInitialBalance[idx].IBEndTime == iBEndTime && cacheInitialBalance[idx].IBX1Multiple == iBX1Multiple && cacheInitialBalance[idx].IBX2Multiple == iBX2Multiple && cacheInitialBalance[idx].IBX3Multiple == iBX3Multiple && cacheInitialBalance[idx].EqualsInput(input))
						return cacheInitialBalance[idx];
			return CacheIndicator<Gemify.InitialBalance>(new Gemify.InitialBalance(){ IBStartTime = iBStartTime, IBEndTime = iBEndTime, IBX1Multiple = iBX1Multiple, IBX2Multiple = iBX2Multiple, IBX3Multiple = iBX3Multiple }, input, ref cacheInitialBalance);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Gemify.InitialBalance InitialBalance(DateTime iBStartTime, DateTime iBEndTime, double iBX1Multiple, double iBX2Multiple, double iBX3Multiple)
		{
			return indicator.InitialBalance(Input, iBStartTime, iBEndTime, iBX1Multiple, iBX2Multiple, iBX3Multiple);
		}

		public Indicators.Gemify.InitialBalance InitialBalance(ISeries<double> input , DateTime iBStartTime, DateTime iBEndTime, double iBX1Multiple, double iBX2Multiple, double iBX3Multiple)
		{
			return indicator.InitialBalance(input, iBStartTime, iBEndTime, iBX1Multiple, iBX2Multiple, iBX3Multiple);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Gemify.InitialBalance InitialBalance(DateTime iBStartTime, DateTime iBEndTime, double iBX1Multiple, double iBX2Multiple, double iBX3Multiple)
		{
			return indicator.InitialBalance(Input, iBStartTime, iBEndTime, iBX1Multiple, iBX2Multiple, iBX3Multiple);
		}

		public Indicators.Gemify.InitialBalance InitialBalance(ISeries<double> input , DateTime iBStartTime, DateTime iBEndTime, double iBX1Multiple, double iBX2Multiple, double iBX3Multiple)
		{
			return indicator.InitialBalance(input, iBStartTime, iBEndTime, iBX1Multiple, iBX2Multiple, iBX3Multiple);
		}
	}
}

#endregion
