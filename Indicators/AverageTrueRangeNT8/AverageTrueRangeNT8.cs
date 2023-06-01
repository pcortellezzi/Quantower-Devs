// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace AverageTrueRangeNT8
{
    /// <summary>
    /// An example of blank indicator. Add your code, compile it and use on the charts in the assigned trading terminal.
    /// Information about API you can find here: http://api.quantower.com
    /// Code samples: https://github.com/Quantower/Examples
    /// </summary>
	public class AverageTrueRangeNT8 : Indicator, IWatchlistIndicator
    {
        [InputParameter("Period", 10, 1, 99999, 1, 0)]
        public int Period = 14;

        public int MinHistoryDepths => 1000;

        private double Value;
        private double prevValue;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public AverageTrueRangeNT8()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "AverageTrueRangeNT8";
            this.Description = "Average True Range with NT8 Formula";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("ATR", Color.CadetBlue, 1, LineStyle.Solid);

            // By default indicator will be applied on main window of the chart
            this.SeparateWindow = true;
            this.UpdateType = IndicatorUpdateType.OnBarClose;
            this.IsUpdateTypesSupported = false;
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

            double high0 = this.High();
            double low0 = this.Low();

            if (args.Reason == UpdateReason.HistoricalBar || args.Reason == UpdateReason.NewBar)
                this.prevValue = this.Value;

            if (this.Count < 1)
                this.Value = high0 - low0;
            else
            {
                double close1 = this.Close(1);
                double trueRange = Math.Max(Math.Abs(low0 - close1), Math.Max(high0 - low0, Math.Abs(high0 - close1)));
                this.Value = ((Math.Min(this.Count + 1, this.Period) - 1) * this.prevValue + trueRange) / Math.Min(this.Count + 1, this.Period);
            }
            if (this.Value < 1)
                this.SetValue(Math.Round(this.Value, 3));
            else
                this.SetValue(Math.Round(this.Value, 2));
        }
    }
}
