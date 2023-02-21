#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
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
    [Gui.CategoryOrder("IB Extention 1", 1)]
    [Gui.CategoryOrder("IB Extention 2", 2)]
    [Gui.CategoryOrder("IB Extention 3", 3)]
    [Gui.CategoryOrder("Initial Balance Period", 4)]
    [Gui.CategoryOrder("Options", 5)]
    [Gui.CategoryOrder("Colors", 6)]
    public class InitialBalance : Indicator
	{
		private double IBHigh;
		private double IBLow;

		private double IBHighState;
		private double IBLowState;

		private int IBStartBar;

		private bool IBComplete;
        private List<double> Ranges;

        private double ORHigh;
        private double ORLow;
        private bool ORComplete;

        private double SessionHigh;
        private double SessionLow;

        private SessionIterator sessionIterator;

        private bool IsDebug;

        public InitialBalance()
        {
        }

        protected override void OnStateChange()
		{
			Debug(">>> " + State);

			if (State == State.SetDefaults)
			{
				Description									= @"Displays Initial Balance and Extensions";
				Name										= "\"Initial Balance\"";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
                PaintPriceMarkers = true;

                // Defaults
                IsDebug = false;

                DisplayOR = true;
                ORComplete = false;
                ORLineColor = Brushes.Gray;

                SessionHigh = 0;
                SessionLow = 0;
                DisplaySessionMid = true;
                SessionMidColor = Brushes.Yellow;

                DisplayIBRange = true;
				HighlightIBPeriod = true;

                IBStartBar = -1;
				IBComplete = false;

                // Default Initial Balance set between 9:30 to 10:30 AM EST
                IBStartTime = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
                IBEndTime = DateTime.Parse("10:30", System.Globalization.CultureInfo.InvariantCulture);
                SessionEndTime = DateTime.Parse("16:15", System.Globalization.CultureInfo.InvariantCulture);

                IBHighState = 0;
                IBLowState = 0;

				IBFillColor	= Brushes.Blue;
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

            }
            else if (State == State.Configure)
			{
				Debug("Adding Seconds Data Series");
				// Add seconds based bars to determine Initial Balance
                AddDataSeries(BarsPeriodType.Second, 30);

                AddPlot(Brushes.Transparent, "IBH");
                AddPlot(Brushes.Transparent, "IBMid");
                AddPlot(Brushes.Transparent, "IBL");
                
                AddPlot(Brushes.Transparent, "ORH");
                AddPlot(Brushes.Transparent, "ORL");

                Ranges = new List<double>();
            }
            else if (State == State.DataLoaded)
            {
                //stores the sessions once bars are ready, but before OnBarUpdate is called
                sessionIterator = new SessionIterator(Bars);
            }
        }

		private void Debug(String message) {
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
            DateTime now = Times[1][0]; // 30 second bar based time
            DateTime StartTime = new DateTime(now.Year, now.Month, now.Day, IBStartTime.Hour, IBStartTime.Minute, IBStartTime.Second, DateTimeKind.Local);
            DateTime EndTime = new DateTime(now.Year, now.Month, now.Day, IBEndTime.Hour, IBEndTime.Minute, IBEndTime.Second, DateTimeKind.Local);
            // Add 30 seconds to the start time
            DateTime OREndTime = StartTime.AddSeconds(30);

            // Calculate session end time
            DateTime TodaySessionEndTime = new DateTime(now.Year, now.Month, now.Day, SessionEndTime.Hour, SessionEndTime.Minute, SessionEndTime.Second, DateTimeKind.Local);

            // Nothing to do if we're outside of working hours.
            if (!(StartTime <= now && now <= TodaySessionEndTime)) return;

            // Session mid value
            // SessionMid[0] = Instrument.MasterInstrument.RoundToTickSize(Low[LowestBar(Low, Bars.BarsSinceNewTradingDay)] + High[HighestBar(High, Bars.BarsSinceNewTradingDay)] / 2.0);
            SessionHigh = Math.Max(SessionHigh, High[0]);
            SessionLow = Math.Min(SessionLow, Low[0]);
            double sessionMid = Instrument.MasterInstrument.RoundToTickSize((SessionHigh + SessionLow) / 2.0);
            Debug("SessionMid is " + sessionMid);

            // Flag that indicates that IB is complete.
            //    - We have an IB start bar, and
            //    - we're past the IB end time
            IBComplete = IBStartBar != -1 && now >= EndTime;

            // Calculate highest and lowest prices if current time is between Start and End
            if (!IBComplete && StartTime <= now &&  now <= EndTime)
			{                
				// Keep track of the bar# (of the 1-minute series)
				// when the IB started. We'll use this later to
				// display the range text.
				IBStartBar = IBStartBar == -1 ? CurrentBars[0] : IBStartBar;
				
				// Calculate the Highest and Lowest prices of the IB
                IBHigh = Math.Max(IBHigh, High[0]);
                IBLow = Math.Min(IBLow, Low[0]);
                double mid = Instrument.MasterInstrument.RoundToTickSize((IBHigh + IBLow) / 2.0);

                Values[0][0] = IBHigh;
                Values[1][0] = mid; 
                Values[2][0] = IBLow;                

                Debug("Time: " + now + ". ORH: " + IBHigh + ", ORL: " + IBLow);
            }

            ORComplete = IBStartBar != -1 && now > OREndTime;

            // If OR calculation is NOT complete and we're still within the OR timeframe
            if (!ORComplete && StartTime <= now && now <= EndTime)
            {
                Debug("Calculating Opening Range.");                                
                
                // Calculate the Highest and Lowest prices of the OR
                ORHigh = Math.Max(ORHigh, High[0]);
                ORLow = Math.Min(ORLow, Low[0]);
                Values[3][0] = ORHigh;
                Values[4][0] = ORLow;

                Debug("Time: " + now + ". ORH: " + ORHigh + ", ORL: " + ORLow);
            }

            String tag = GenerateTodayTag(now);

            // Draw the IB Rectangle if:
            //    - we have an Initial Balance, and
            //    - the session is still open, and
            //	  - the IB high/low has changed (compare prev state - prevents unnecessary drawing cycles)
            if (IBHigh > double.MinValue && IBLow < double.MaxValue && 
				now <= TodaySessionEndTime && 
				(IBHighState != IBHigh || IBLowState != IBLow))
			{

                SetZOrder(-1);

                // Draw the IB range for the entire session
                Draw.Rectangle(this, "IB_Session" + tag, false, StartTime, IBHigh, TodaySessionEndTime, IBLow, IBFillColor, IBFillColor, IBFillOpacity);

				// Highlight IB Period if desired
				if (HighlightIBPeriod)
				{
					Draw.Rectangle(this, "IB" + tag, false, StartTime, IBHigh, EndTime, IBLow, IBHighlightColor, IBHighlightColor, IBHighlightOpacity);
				}

				// Save IB high/low state for comparing during the next update
				IBHighState = IBHigh;
				IBLowState = IBLow;
			}

            // Calculate and display IB range if desired
            if (DisplayIBRange)
            {
                double range = Instrument.MasterInstrument.RoundToTickSize(IBHigh - IBLow);
                Debug("Time: " + now + ", Range is : " + range);

                // Keep track of ranges
                if (IBComplete) Ranges.Add(range);

                Debug("Calculating range");
                // Find the current median range
                double medianRange = CalculateMedian(Ranges);
                Debug("Median range: " + medianRange);

                double y = IBHigh + (2 * Instrument.MasterInstrument.TickSize);
                SetZOrder(int.MaxValue);

                if (!IBComplete)
                {
                    Debug("Developing IB...");
                    TimeSpan IBDuration = EndTime - StartTime;
                    TimeSpan Elapsed = now - StartTime;
                    double Progress = (double)Elapsed.Ticks / (double)IBDuration.Ticks;
                    Debug("Percentage complete: " + Progress);
                    Draw.Text(this, "IBR" + tag, false, "IB Forming (" + Progress.ToString("P", CultureInfo.InvariantCulture) + "). Current Range: " + range.ToString() + ", Median: " + String.Format("{0:0.0#}", medianRange), StartTime, y, 10, TextColor, TextFont, TextAlignment.Left, null, null, 100);
                    Debug("Done drawing progress text.");
                }
                else
                {
                    Debug("Time: " + now + ", Range is : " + range);
                    Debug("IB Complete...");
                    Draw.Text(this, "IBR" + tag, false, "IB Range: " + range.ToString() + ", Median: " + String.Format("{0:0.0#}", medianRange), StartTime, y, 10, TextColor, TextFont, TextAlignment.Left, null, null, 100);
                    Debug("Done drawing range text.");
                }
            }

            if (DisplayOR && ORComplete)
            {
                Draw.Text(this, "ORHText" + tag, false, "ORH", StartTime, (ORHigh + Instrument.MasterInstrument.TickSize), 10, ORLineColor, TextFont, TextAlignment.Left, null, null, 100);
                Draw.Line(this, "ORHigh" + tag, false, StartTime, ORHigh, TodaySessionEndTime, ORHigh, ORLineColor, DashStyleHelper.DashDot, 1);
                Draw.Line(this, "ORLow" + tag, false, StartTime, ORLow, TodaySessionEndTime, ORLow, ORLineColor, DashStyleHelper.DashDot, 1);
                Draw.Text(this, "ORLText" + tag, false, "ORL", StartTime, (ORLow - Instrument.MasterInstrument.TickSize), -10, ORLineColor, TextFont, TextAlignment.Left, null, null, 100);
            }

            if (DisplaySessionMid)
            {
                // Draw Session Mid
                Draw.Text(this, "SessionMidText", false, "Session Mid", StartTime, (sessionMid + Instrument.MasterInstrument.TickSize), 10, SessionMidColor, TextFont, TextAlignment.Left, null, null, 100);
                Draw.Line(this, "SessionMid", false, StartTime, sessionMid, TodaySessionEndTime, sessionMid, SessionMidColor, DashStyleHelper.DashDotDot, 1);
            }

            // Draw Extensions if IB is complete
            if (IBComplete)
			{
                // Calculate range for extensions
                double range = Instrument.MasterInstrument.RoundToTickSize(IBHigh - IBLow);

                int barsAgo = (CurrentBars[0] - IBStartBar);

                if (DisplayIBX1)
				{
                    double offset = range * (IBX1Multiple - 1.0);
                    Debug("IBX1 - Range : " + range + ", offset : " + offset);

                    double upper = IBHigh + offset;
                    double lower = IBLow - offset;

					Draw.Line(this, "IBX1Up" + tag, false, StartTime, upper, TodaySessionEndTime, upper, IBX1Color, IBX1DashStyle, IBX1Width);
                    Draw.Line(this, "IBX1Down" + tag, false, StartTime, lower, TodaySessionEndTime, lower, IBX1Color, IBX1DashStyle, IBX1Width);

                    String multiple = String.Format("{0:0.0#}", IBX1Multiple);
                    Draw.Text(this, "IBX1UpText" + tag, false, "IBx" + multiple, StartTime, (upper + Instrument.MasterInstrument.TickSize), 10, IBX1Color, TextFont, TextAlignment.Left, null, null, 100);
                    Draw.Text(this, "IBX1DownText" + tag, false, "IBx" + multiple, StartTime, (lower + Instrument.MasterInstrument.TickSize), 10, IBX1Color, TextFont, TextAlignment.Left, null, null, 100);
                }

                if (DisplayIBX2)
                {
                    double offset = range * (IBX2Multiple - 1.0);
                    Debug("IBX2 - Range : " + range + ", offset : " + offset);

                    double upper = IBHigh + offset;
                    double lower = IBLow - offset;

                    Draw.Line(this, "IBX2Up" + tag, false, StartTime, upper, TodaySessionEndTime, upper, IBX2Color, IBX2DashStyle, IBX2Width);
                    Draw.Line(this, "IBX2Down" + tag, false, StartTime, lower, TodaySessionEndTime, lower, IBX2Color, IBX2DashStyle, IBX2Width);

                    String multiple = String.Format("{0:0.0#}", IBX2Multiple);
                    Draw.Text(this, "IBX2UpText" + tag, false, "IBx" + multiple, StartTime, (upper + Instrument.MasterInstrument.TickSize), 10, IBX2Color, TextFont, TextAlignment.Left, null, null, 100);
                    Draw.Text(this, "IBX2DownText" + tag, false, "IBx" + multiple, StartTime, (lower + Instrument.MasterInstrument.TickSize), 10, IBX2Color, TextFont, TextAlignment.Left, null, null, 100);

                }

                if (DisplayIBX3)
                {
                    double offset = range * (IBX3Multiple - 1.0);
                    Debug("IBX3 - Range : " + range + ", offset : " + offset);

                    double upper = IBHigh + offset;
                    double lower = IBLow - offset;

                    Draw.Line(this, "IB31Up" + tag, false, StartTime, upper, TodaySessionEndTime, upper, IBX3Color, IBX3DashStyle, IBX3Width);
                    Draw.Line(this, "IB31Down" + tag, false, StartTime, lower, TodaySessionEndTime, lower, IBX3Color, IBX3DashStyle, IBX3Width);

                    String multiple = String.Format("{0:0.0#}", IBX3Multiple);
                    Draw.Text(this, "IBX3UpText" + tag, false, "IBx" + multiple, StartTime, (upper + Instrument.MasterInstrument.TickSize), 10, IBX3Color, TextFont, TextAlignment.Left, null, null, 100);
                    Draw.Text(this, "IBX3DownText" + tag, false, "IBx" + multiple, StartTime, (lower + Instrument.MasterInstrument.TickSize), 10, IBX3Color, TextFont, TextAlignment.Left, null, null, 100);
                }
            }
        }

        private void Reset()
        {
            Debug("First bar of session. Resetting OR values.");

            // Reset values
            IBHigh = double.MinValue;
            IBLow = double.MaxValue;
            IBHighState = 0;
            IBLowState = 0;
            IBStartBar = -1;
            IBComplete = false;

            ORComplete = false;
            ORHigh = double.MinValue;
            ORLow = double.MaxValue;

            SessionHigh = double.MinValue;
            SessionLow = double.MaxValue; 
        }

        private String GenerateTodayTag (DateTime now)
		{
			return "_" + now.Month + now.Day + now.Year;
        }

        private double CalculateMedian (List<double> ranges)
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

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Session End Time", Description = "Session end time", Order = 300, GroupName = "Initial Balance Period")]
        public DateTime SessionEndTime
        { get; set; }

        // ----------- Options

        [Display(Name = "Highlight IB Period", Description = "Highlight IB Period", Order = 100, GroupName = "Options")]
        public bool HighlightIBPeriod
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Display IB Range Text", Description = "Display IB Range Text", Order = 200, GroupName = "Options")]
        public bool DisplayIBRange
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Opening Range (30 sec)", Description = "Display Opening Range (30 seconds)", Order = 300, GroupName = "Options")]
        public bool DisplayOR
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Session Mid", Description = "Display Session Mid", Order = 400, GroupName = "Options")]
        public bool DisplaySessionMid
        { get; set; }


        // ----------- IB Extensions

        [NinjaScriptProperty]
        [Display(Name = "Display IB Extension", Description = "Display IB Extension", Order = 100, GroupName = "IB Extension 1")]
        public bool DisplayIBX1
        { get; set; }

        [NinjaScriptProperty]
		[Range(1.0, double.MaxValue)]
        [Display(Name = "IB Extension Multiple", Description = "IB Extension Multiple", Order = 200, GroupName = "IB Extension 1")]
        public double IBX1Multiple
        { get; set; }

        [NinjaScriptProperty]
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

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "IB Extension Line DashStyle", Description = "IB Extension line dash style", Order = 400, GroupName = "IB Extension 1")]
        public DashStyleHelper IBX1DashStyle
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Range(1,10)]
        [Display(Name = "IB Extension Line Thickness", Description = "IB Extension line thickness", Order = 500, GroupName = "IB Extension 1")]
        public int IBX1Width
        { get; set; }




        [NinjaScriptProperty]
        [Display(Name = "Display IB Extension", Description = "Display IB Extension", Order = 100, GroupName = "IB Extension 2")]
        public bool DisplayIBX2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "IB Extension Multiple", Description = "IB Extension Multiple", Order = 200, GroupName = "IB Extension 2")]
        public double IBX2Multiple
        { get; set; }

        [NinjaScriptProperty]
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

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "IB Extension Line DashStyle", Description = "IB Extension line dash style", Order = 400, GroupName = "IB Extension 2")]
        public DashStyleHelper IBX2DashStyle
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Range(1, 10)]
        [Display(Name = " Extension Line Thickness", Description = "IB Extension line thickness", Order = 500, GroupName = "IB Extension 2")]
        public int IBX2Width
        { get; set; }



        [NinjaScriptProperty]
        [Display(Name = "Display IB Extension", Description = "Display IB Extension", Order = 100, GroupName = "IB Extension 3")]
        public bool DisplayIBX3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "IB Extension Multiple", Description = "IB Extension Multiple", Order = 200, GroupName = "IB Extension 3")]
        public double IBX3Multiple
        { get; set; }

        [NinjaScriptProperty]
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

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "IB Extension Line DashStyle", Description = "IB Extension line dash style", Order = 400, GroupName = "IB Extension 3")]
        public DashStyleHelper IBX3DashStyle
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Range(1, 10)]
        [Display(Name = " Extension Line Thickness", Description = "IB Extension line thickness", Order = 500, GroupName = "IB Extension 3")]
        public int IBX3Width
        { get; set; }

        // ----------- Colors

        [NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Initial Balance Fill Color", Description="Initial Balance fill color", Order=100, GroupName = "Colors")]
		public Brush IBFillColor
		{ get; set; }

		[Browsable(false)]
		public string IBFillColorSerializable
		{
			get { return Serialize.BrushToString(IBFillColor); }
			set { IBFillColor = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Opacity", Description="Opacity of Initial Balance", Order=200, GroupName = "Colors")]
		public int IBFillOpacity
		{ get; set; }

        [NinjaScriptProperty]
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

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "IB Highlight Opacity", Description = "Initial Balance hightlight opacity", Order = 400, GroupName = "Colors")]
        public int IBHighlightOpacity
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "IB Range Text Color", Description = "Color to use to display IB range", Order = 500, GroupName = "Colors")]
        public Brush TextColor
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "IB Range Text Font", Description = "Font to use to display IB range", Order = 600, GroupName = "Colors")]
        public SimpleFont TextFont
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Opening Range Lines Color", Description = "Opening Range lines color", Order = 700, GroupName = "Colors")]
        public Brush ORLineColor
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Session Mid Line Color", Description = "Session Mid line color", Order = 800, GroupName = "Colors")]
        public Brush SessionMidColor
        { get; set; }


        [Browsable(false)]
        [XmlIgnore()]
        public bool IsIBComplete
        {
            get { return IBComplete; }
        }

        #region Series

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> IBH
        {
            get { return Values[0]; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> IBMid
        {
            get { return Values[1]; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> IBL
        {
            get { return Values[2]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> ORH
        {
            get { return Values[3]; }
        }
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> ORL
        {
            get { return Values[4]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> SessionMid
        {
            get { return Values[5]; }
        }

        #endregion

        #endregion

    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Gemify.InitialBalance[] cacheInitialBalance;
		public Gemify.InitialBalance InitialBalance(DateTime iBStartTime, DateTime iBEndTime, DateTime sessionEndTime, bool displayIBRange, bool displayOR, bool displaySessionMid, bool displayIBX1, double iBX1Multiple, Brush iBX1Color, DashStyleHelper iBX1DashStyle, int iBX1Width, bool displayIBX2, double iBX2Multiple, Brush iBX2Color, DashStyleHelper iBX2DashStyle, int iBX2Width, bool displayIBX3, double iBX3Multiple, Brush iBX3Color, DashStyleHelper iBX3DashStyle, int iBX3Width, Brush iBFillColor, int iBFillOpacity, Brush iBHighlightColor, int iBHighlightOpacity, Brush textColor, SimpleFont textFont, Brush oRLineColor, Brush sessionMidColor)
		{
			return InitialBalance(Input, iBStartTime, iBEndTime, sessionEndTime, displayIBRange, displayOR, displaySessionMid, displayIBX1, iBX1Multiple, iBX1Color, iBX1DashStyle, iBX1Width, displayIBX2, iBX2Multiple, iBX2Color, iBX2DashStyle, iBX2Width, displayIBX3, iBX3Multiple, iBX3Color, iBX3DashStyle, iBX3Width, iBFillColor, iBFillOpacity, iBHighlightColor, iBHighlightOpacity, textColor, textFont, oRLineColor, sessionMidColor);
		}

		public Gemify.InitialBalance InitialBalance(ISeries<double> input, DateTime iBStartTime, DateTime iBEndTime, DateTime sessionEndTime, bool displayIBRange, bool displayOR, bool displaySessionMid, bool displayIBX1, double iBX1Multiple, Brush iBX1Color, DashStyleHelper iBX1DashStyle, int iBX1Width, bool displayIBX2, double iBX2Multiple, Brush iBX2Color, DashStyleHelper iBX2DashStyle, int iBX2Width, bool displayIBX3, double iBX3Multiple, Brush iBX3Color, DashStyleHelper iBX3DashStyle, int iBX3Width, Brush iBFillColor, int iBFillOpacity, Brush iBHighlightColor, int iBHighlightOpacity, Brush textColor, SimpleFont textFont, Brush oRLineColor, Brush sessionMidColor)
		{
			if (cacheInitialBalance != null)
				for (int idx = 0; idx < cacheInitialBalance.Length; idx++)
					if (cacheInitialBalance[idx] != null && cacheInitialBalance[idx].IBStartTime == iBStartTime && cacheInitialBalance[idx].IBEndTime == iBEndTime && cacheInitialBalance[idx].SessionEndTime == sessionEndTime && cacheInitialBalance[idx].DisplayIBRange == displayIBRange && cacheInitialBalance[idx].DisplayOR == displayOR && cacheInitialBalance[idx].DisplaySessionMid == displaySessionMid && cacheInitialBalance[idx].DisplayIBX1 == displayIBX1 && cacheInitialBalance[idx].IBX1Multiple == iBX1Multiple && cacheInitialBalance[idx].IBX1Color == iBX1Color && cacheInitialBalance[idx].IBX1DashStyle == iBX1DashStyle && cacheInitialBalance[idx].IBX1Width == iBX1Width && cacheInitialBalance[idx].DisplayIBX2 == displayIBX2 && cacheInitialBalance[idx].IBX2Multiple == iBX2Multiple && cacheInitialBalance[idx].IBX2Color == iBX2Color && cacheInitialBalance[idx].IBX2DashStyle == iBX2DashStyle && cacheInitialBalance[idx].IBX2Width == iBX2Width && cacheInitialBalance[idx].DisplayIBX3 == displayIBX3 && cacheInitialBalance[idx].IBX3Multiple == iBX3Multiple && cacheInitialBalance[idx].IBX3Color == iBX3Color && cacheInitialBalance[idx].IBX3DashStyle == iBX3DashStyle && cacheInitialBalance[idx].IBX3Width == iBX3Width && cacheInitialBalance[idx].IBFillColor == iBFillColor && cacheInitialBalance[idx].IBFillOpacity == iBFillOpacity && cacheInitialBalance[idx].IBHighlightColor == iBHighlightColor && cacheInitialBalance[idx].IBHighlightOpacity == iBHighlightOpacity && cacheInitialBalance[idx].TextColor == textColor && cacheInitialBalance[idx].TextFont == textFont && cacheInitialBalance[idx].ORLineColor == oRLineColor && cacheInitialBalance[idx].SessionMidColor == sessionMidColor && cacheInitialBalance[idx].EqualsInput(input))
						return cacheInitialBalance[idx];
			return CacheIndicator<Gemify.InitialBalance>(new Gemify.InitialBalance(){ IBStartTime = iBStartTime, IBEndTime = iBEndTime, SessionEndTime = sessionEndTime, DisplayIBRange = displayIBRange, DisplayOR = displayOR, DisplaySessionMid = displaySessionMid, DisplayIBX1 = displayIBX1, IBX1Multiple = iBX1Multiple, IBX1Color = iBX1Color, IBX1DashStyle = iBX1DashStyle, IBX1Width = iBX1Width, DisplayIBX2 = displayIBX2, IBX2Multiple = iBX2Multiple, IBX2Color = iBX2Color, IBX2DashStyle = iBX2DashStyle, IBX2Width = iBX2Width, DisplayIBX3 = displayIBX3, IBX3Multiple = iBX3Multiple, IBX3Color = iBX3Color, IBX3DashStyle = iBX3DashStyle, IBX3Width = iBX3Width, IBFillColor = iBFillColor, IBFillOpacity = iBFillOpacity, IBHighlightColor = iBHighlightColor, IBHighlightOpacity = iBHighlightOpacity, TextColor = textColor, TextFont = textFont, ORLineColor = oRLineColor, SessionMidColor = sessionMidColor }, input, ref cacheInitialBalance);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Gemify.InitialBalance InitialBalance(DateTime iBStartTime, DateTime iBEndTime, DateTime sessionEndTime, bool displayIBRange, bool displayOR, bool displaySessionMid, bool displayIBX1, double iBX1Multiple, Brush iBX1Color, DashStyleHelper iBX1DashStyle, int iBX1Width, bool displayIBX2, double iBX2Multiple, Brush iBX2Color, DashStyleHelper iBX2DashStyle, int iBX2Width, bool displayIBX3, double iBX3Multiple, Brush iBX3Color, DashStyleHelper iBX3DashStyle, int iBX3Width, Brush iBFillColor, int iBFillOpacity, Brush iBHighlightColor, int iBHighlightOpacity, Brush textColor, SimpleFont textFont, Brush oRLineColor, Brush sessionMidColor)
		{
			return indicator.InitialBalance(Input, iBStartTime, iBEndTime, sessionEndTime, displayIBRange, displayOR, displaySessionMid, displayIBX1, iBX1Multiple, iBX1Color, iBX1DashStyle, iBX1Width, displayIBX2, iBX2Multiple, iBX2Color, iBX2DashStyle, iBX2Width, displayIBX3, iBX3Multiple, iBX3Color, iBX3DashStyle, iBX3Width, iBFillColor, iBFillOpacity, iBHighlightColor, iBHighlightOpacity, textColor, textFont, oRLineColor, sessionMidColor);
		}

		public Indicators.Gemify.InitialBalance InitialBalance(ISeries<double> input , DateTime iBStartTime, DateTime iBEndTime, DateTime sessionEndTime, bool displayIBRange, bool displayOR, bool displaySessionMid, bool displayIBX1, double iBX1Multiple, Brush iBX1Color, DashStyleHelper iBX1DashStyle, int iBX1Width, bool displayIBX2, double iBX2Multiple, Brush iBX2Color, DashStyleHelper iBX2DashStyle, int iBX2Width, bool displayIBX3, double iBX3Multiple, Brush iBX3Color, DashStyleHelper iBX3DashStyle, int iBX3Width, Brush iBFillColor, int iBFillOpacity, Brush iBHighlightColor, int iBHighlightOpacity, Brush textColor, SimpleFont textFont, Brush oRLineColor, Brush sessionMidColor)
		{
			return indicator.InitialBalance(input, iBStartTime, iBEndTime, sessionEndTime, displayIBRange, displayOR, displaySessionMid, displayIBX1, iBX1Multiple, iBX1Color, iBX1DashStyle, iBX1Width, displayIBX2, iBX2Multiple, iBX2Color, iBX2DashStyle, iBX2Width, displayIBX3, iBX3Multiple, iBX3Color, iBX3DashStyle, iBX3Width, iBFillColor, iBFillOpacity, iBHighlightColor, iBHighlightOpacity, textColor, textFont, oRLineColor, sessionMidColor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Gemify.InitialBalance InitialBalance(DateTime iBStartTime, DateTime iBEndTime, DateTime sessionEndTime, bool displayIBRange, bool displayOR, bool displaySessionMid, bool displayIBX1, double iBX1Multiple, Brush iBX1Color, DashStyleHelper iBX1DashStyle, int iBX1Width, bool displayIBX2, double iBX2Multiple, Brush iBX2Color, DashStyleHelper iBX2DashStyle, int iBX2Width, bool displayIBX3, double iBX3Multiple, Brush iBX3Color, DashStyleHelper iBX3DashStyle, int iBX3Width, Brush iBFillColor, int iBFillOpacity, Brush iBHighlightColor, int iBHighlightOpacity, Brush textColor, SimpleFont textFont, Brush oRLineColor, Brush sessionMidColor)
		{
			return indicator.InitialBalance(Input, iBStartTime, iBEndTime, sessionEndTime, displayIBRange, displayOR, displaySessionMid, displayIBX1, iBX1Multiple, iBX1Color, iBX1DashStyle, iBX1Width, displayIBX2, iBX2Multiple, iBX2Color, iBX2DashStyle, iBX2Width, displayIBX3, iBX3Multiple, iBX3Color, iBX3DashStyle, iBX3Width, iBFillColor, iBFillOpacity, iBHighlightColor, iBHighlightOpacity, textColor, textFont, oRLineColor, sessionMidColor);
		}

		public Indicators.Gemify.InitialBalance InitialBalance(ISeries<double> input , DateTime iBStartTime, DateTime iBEndTime, DateTime sessionEndTime, bool displayIBRange, bool displayOR, bool displaySessionMid, bool displayIBX1, double iBX1Multiple, Brush iBX1Color, DashStyleHelper iBX1DashStyle, int iBX1Width, bool displayIBX2, double iBX2Multiple, Brush iBX2Color, DashStyleHelper iBX2DashStyle, int iBX2Width, bool displayIBX3, double iBX3Multiple, Brush iBX3Color, DashStyleHelper iBX3DashStyle, int iBX3Width, Brush iBFillColor, int iBFillOpacity, Brush iBHighlightColor, int iBHighlightOpacity, Brush textColor, SimpleFont textFont, Brush oRLineColor, Brush sessionMidColor)
		{
			return indicator.InitialBalance(input, iBStartTime, iBEndTime, sessionEndTime, displayIBRange, displayOR, displaySessionMid, displayIBX1, iBX1Multiple, iBX1Color, iBX1DashStyle, iBX1Width, displayIBX2, iBX2Multiple, iBX2Color, iBX2DashStyle, iBX2Width, displayIBX3, iBX3Multiple, iBX3Color, iBX3DashStyle, iBX3Width, iBFillColor, iBFillOpacity, iBHighlightColor, iBHighlightOpacity, textColor, textFont, oRLineColor, sessionMidColor);
		}
	}
}

#endregion
