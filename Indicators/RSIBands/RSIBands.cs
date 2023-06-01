// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace RSIBands
{
    /// <summary>
    /// An example of blank indicator. Add your code, compile it and use on the charts in the assigned trading terminal.
    /// Information about API you can find here: http://api.quantower.com
    /// Code samples: https://github.com/Quantower/Examples
    /// </summary>
	public class RSIBands : Indicator
    {
        private double PUp = 0;
        private double NUp = 0;
        private double PLow = 0;
        private double NLow = 0;
        private double PUpOld = 0;
        private double NUpOld = 0;
        private double PLowOld = 0;
        private double NLowOld = 0;
        
        private double diff = 0;
        private double W = 0;
        private double S = 0;
        private double HypotheticalCloseToMatchRSITarget = 0;

        [InputParameter("Period", 10, 1, 99999, 1, 0)]
        public int Period = 14;

        [InputParameter("RSI Upper Level", 20, 1, 99999, 1, 1)]
        public int RSIUpperLevel = 70;

        [InputParameter("RSI Lower Level", 30, 1, 99999, 1, 1)]
        public int RSILowerLevel = 30;

        public override string ShortName => $"{this.Name} ({this.Period}: {this.RSIUpperLevel}/{this.RSILowerLevel})";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public RSIBands()
            : base()
        {
            // Defines indicator's name and description.
            Name = "RSIBands";
            Description = @"Francois Bertrand's RSI Bands as outlined in Stocks & Commodities April 2008 issue.";

            // Defines line on demand with particular parameters.
            AddLineSeries("RSIBandUpper", Color.CadetBlue, 1, LineStyle.Solid);
            AddLineSeries("RSIBandLower", Color.CadetBlue, 1, LineStyle.Solid);

            // By default indicator will be applied on main window of the chart
            SeparateWindow = false;
            UpdateType = IndicatorUpdateType.OnBarClose;
            IsUpdateTypesSupported = false;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Add your initialization code here
        }

        /// <summary>
        /// Calculation entry point. This function is called when a price data updates. 
        /// Will be runing under the HistoricalBar mode during history loading. 
        /// Under NewTick during realtime. 
        /// Under NewBar if start of the new bar is required.
        /// </summary>
        /// <param name="args">Provides data of updating reason and incoming price.</param>
        protected override void OnUpdate(UpdateArgs args)
        {

            if (this.Count < 1)
                return;

            if (args.Reason != UpdateReason.NewTick)
            {
                this.PUpOld = this.PUp;
                this.NUpOld = this.NUp;
                this.PLowOld = this.PLow;
                this.NLowOld = this.NLow;
            }


            SetValue(BuiltInRSIequivalent(this.RSIUpperLevel, this.PUpOld, this.NUpOld, GetValue(1)));
            SetValue(BuiltInRSIequivalent(this.RSILowerLevel, this.PLowOld, this.NLowOld, GetValue(1, 1)), 1);
        }

        private double BuiltInRSIequivalent(int TargetRSILevel, double P, double N, double PrevRSIBand)
        {
            this.W = 0;
            this.S = 0;

            this.diff = this.Close(0) - this.Close(1);

            if (this.diff > 0)
                this.W = this.diff;
            else if (this.diff < 0)
                this.S = -this.diff;

            if (PrevRSIBand > this.Close(1))
                this.HypotheticalCloseToMatchRSITarget = this.Close(1) + P - P * this.Period - ((N * this.Period) - N) * TargetRSILevel / (TargetRSILevel - 100);
            else
                this.HypotheticalCloseToMatchRSITarget = this.Close(1) - N - P + N * this.Period + P * this.Period + (100 * P) / TargetRSILevel - (100 * P * this.Period) / TargetRSILevel;

            if (PrevRSIBand == GetValue(1))
            {
                this.PUp = ((this.Period - 1) * P + this.W) / this.Period;
                this.NUp = ((this.Period - 1) * N + this.S) / this.Period;
            }
            else if (PrevRSIBand == GetValue(1, 1))
            {
                this.PLow = ((this.Period - 1) * P + this.W) / this.Period;
                this.NLow = ((this.Period - 1) * N + this.S) / this.Period;
            }

            return HypotheticalCloseToMatchRSITarget;
        }
    }
}
