#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using System;
using System.ComponentModel.DataAnnotations;
using InitialBalance = NinjaTrader.NinjaScript.Indicators.Gemify.InitialBalance;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class IBTestStrategy : Strategy
	{
        // ================= INITIAL BALANCE : README ==================
        // This is the initial balance indicator instance
        // that will be used in the strategy.
        private InitialBalance initialBalanceIndicator;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "IBTestStrategy";
				Calculate									= Calculate.OnPriceChange;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;


                // ================= INITIAL BALANCE : README ==================
                // Default Initial Balance set between 9:30 to 10:30 AM EST
                IBStartTime = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
                IBEndTime = DateTime.Parse("10:30", System.Globalization.CultureInfo.InvariantCulture);
				// Default extension ratios
				IBX1Multiple = 1.5;
                IBX2Multiple = 2.0;
                IBX3Multiple = 3.0;

            }
            else if (State == State.Configure)
			{
                // ================= INITIAL BALANCE : README ==================
                // This data series is required by the InitialBalance indicator
                // to compute it's values.
                AddDataSeries(Data.BarsPeriodType.Second, 30);

			}
			else if (State == State.DataLoaded)
			{
                // ================= INITIAL BALANCE : README ==================
				// This is the Initial Balance indicator instance we'll use in the strategy
                initialBalanceIndicator = InitialBalance(Close, IBStartTime, IBEndTime, IBX1Multiple, IBX2Multiple, IBX3Multiple);
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0 || CurrentBars[0] < 1) return; 

            // ================= INITIAL BALANCE : README ==================
            // It is important to remember to call the Update() on the indicator
			// before using it in the strategy logic.
            initialBalanceIndicator.Update();



            // ================= INITIAL BALANCE : README ==================
            // If you're relying on the IB to be complete before
			// executing any trades, make sure you check for the "IsIBComplete" property.
            // Similar logic applies to using the "IsORComplete" property for the Opening Range.
            if (initialBalanceIndicator.IsIBComplete)
			{

                // =========================================================================
                // Here are the values that are available from the InitialBalance indicator
                // for use in the strategy:
                // =========================================================================

                bool IsIBComplete = initialBalanceIndicator.IsIBComplete; // Is the Initial Balance complete?
                bool IsORComplete = initialBalanceIndicator.IsORComplete; // Is the Opening Range complete?

                double IBH = initialBalanceIndicator.IBHigh;		// Initial Balance High
				double IBL = initialBalanceIndicator.IBLow;		// Initial Balance Low
				double SMid = initialBalanceIndicator.SessionMid;// Session Mid

				double IBX1Upper = initialBalanceIndicator.IBX1Upper; // IB Extension 1 Upper
                double IBX1Lower = initialBalanceIndicator.IBX1Lower; // IB Extension 1 Lower

                double IBX2Upper = initialBalanceIndicator.IBX2Upper; // IB Extension 2 Upper
                double IBX2Lower = initialBalanceIndicator.IBX2Lower; // IB Extension 2 Lower

                double IBX3Upper = initialBalanceIndicator.IBX3Upper; // IB Extension 3 Upper
                double IBX3Lower = initialBalanceIndicator.IBX3Lower; // IB Extension 3 Lower

                double ORH = initialBalanceIndicator.ORHigh;		// Opening Range High
				double ORL = initialBalanceIndicator.ORLow;		// Opening Range Low

                Print("IB is [" + IBH + " to " + IBL + "]. Current Range is " + (IBH - IBL));
                Print("IB Extension 1 Upper : " + IBX1Upper);
                Print("IB Extension 2 Lower : " + IBX2Lower);
            }

            // =========================================================================
            // WARNING: This code is intended as a SAMPLE. DO NOT USE THIS STRATEGY AS-IS.
            // =========================================================================

            // EXAMPLE entry
            // 
            // If IB is complete AND
            // current Close crosses below IBL (IB low) in the last 1 bars
            // enter short
            if (initialBalanceIndicator.IsIBComplete && CrossBelow(Close, initialBalanceIndicator.IBLow, 1))
			{
				Print("--> SHORT ENTRY");
				EnterShort(Convert.ToInt32(DefaultQuantity));
			}

            // EXAMPLE entry
            // 
            // If IB is complete AND
            //		current Close crosses up above IB High in the last 1 bars
            // enter long
            if (initialBalanceIndicator.IsIBComplete &&  CrossAbove(Close, initialBalanceIndicator.IBHigh, 1))
            {
                Print("--> LONG ENTRY");
                EnterLong(Convert.ToInt32(DefaultQuantity));
            }

            // =========================================================================
            // WARNING: NO EXIT LOGIC EXISTS IN THIS SAMPLE
            // =========================================================================

        }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance Begin", Description = "Initial balance begin time", Order = 100, GroupName = "Parameters")]
        public DateTime IBStartTime
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance End", Description = "Initial balance end time", Order = 200, GroupName = "Parameters")]
        public DateTime IBEndTime
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "IB Ext.1 Multiple", Description = "Initial Balance Extension 1 Multiple", Order = 300, GroupName = "Parameters")]
        public double IBX1Multiple
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "IB Ext.1 Multiple", Description = "Initial Balance Extension 2 Multiple", Order = 400, GroupName = "Parameters")]
        public double IBX2Multiple
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "IB Ext.1 Multiple", Description = "Initial Balance Extension 3 Multiple", Order = 500, GroupName = "Parameters")]
        public double IBX3Multiple
        { get; set; }

    }
}
