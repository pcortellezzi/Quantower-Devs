// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace RSIBands {
	public class RSIBands : Indicator {
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

        private HistoricalData mtfData = null;
        private Indicator rsiBands = null;

        [InputParameter("MTFPeriod", 10)]
        public Period MTFPeriod = Period.MIN5;

        [InputParameter("Period", 20, 1, 99999, 1, 0)]
        public int rsiPeriod = 14;

        [InputParameter("RSI Upper Level", 30, 1, 99999, 1, 1)]
        public int RSIUpperLevel = 70;

        [InputParameter("RSI Lower Level", 40, 1, 99999, 1, 1)]
        public int RSILowerLevel = 30;

        public override string ShortName => $"{this.Name} ({this.rsiPeriod}: {this.RSIUpperLevel}/{this.RSILowerLevel} @ {this.MTFPeriod})";
        public override string SourceCodeLink => "https://github.com/pcortellezzi/Quantower-Devs/blob/main/Indicators/RSIBands/RSIBands.cs";

        public RSIBands() : base() {
            // Defines indicator's name and description.
            Name = "RSIBands";
            Description = @"Francois Bertrand's RSI Bands as outlined in Stocks & Commodities April 2008 issue.";

            // Defines line on demand with particular parameters.
            AddLineSeries("RSIBandUpper", Color.CadetBlue, 1, LineStyle.Solid);
            AddLineSeries("RSIBandLower", Color.CadetBlue, 1, LineStyle.Solid);

            // By default indicator will be applied on main window of the chart
            SeparateWindow = false;
            UpdateType = IndicatorUpdateType.OnTick;
            IsUpdateTypesSupported = false;
        }

        public RSIBands(Period MTFPeriod, int rsiPeriod, int RSIUpperLevel, int RSILowerLevel) : this() {
            this.MTFPeriod = MTFPeriod;
            this.rsiPeriod = rsiPeriod;
            this.RSIUpperLevel = RSIUpperLevel;
            this.RSILowerLevel = RSILowerLevel;
        }

        protected override void OnInit() {
            if (MTFPeriod != this.HistoricalData.Period) {
                this.mtfData = this.Symbol.GetHistory(MTFPeriod, this.HistoricalData.FromTime, this.HistoricalData.ToTime);
                this.rsiBands = new RSIBands(this.MTFPeriod, this.rsiPeriod, this.RSIUpperLevel, this.RSILowerLevel);
                this.mtfData.AddIndicator(this.rsiBands);
            }
        }

        protected override void OnUpdate(UpdateArgs args) {
            if (this.Count < 1)
                return;

            if (args.Reason != UpdateReason.NewTick) {
                this.PUpOld = this.PUp;
                this.NUpOld = this.NUp;
                this.PLowOld = this.PLow;
                this.NLowOld = this.NLow;
            }

            if (this.mtfData == null) {
                SetValue(BuiltInRSIequivalent(this.RSIUpperLevel, this.PUpOld, this.NUpOld, GetValue(1), this.Close(0), this.Close(1)));
                SetValue(BuiltInRSIequivalent(this.RSILowerLevel, this.PLowOld, this.NLowOld, GetValue(1, 1), this.Close(0), this.Close(1)), 1);
            }
            else
            {
                int offsetMTFData = (int)this.mtfData.GetIndexByTime(this.Time().Ticks);

                if (offsetMTFData < 0)
                    return;

                if ((this.mtfData[offsetMTFData] as HistoryItemBar).TimeLeft == this.Time())
                {
                    SetValue(this.rsiBands.GetValue(offsetMTFData));
                    SetValue(this.rsiBands.GetValue(offsetMTFData, 1), 1);
                    int offset1 = (int)this.HistoricalData.GetIndexByTime(this.Time().Ticks);
                    int offset2 = (int)this.HistoricalData.GetIndexByTime((this.mtfData[offsetMTFData + 1] as HistoryItemBar).TimeLeft.Ticks);
                    if (offset1 == 0) {
                        for (int i = 1; i < offset2; i++) {
                            SetValue(this.rsiBands.GetValue(offsetMTFData) - (this.rsiBands.GetValue(offsetMTFData) - this.rsiBands.GetValue(offsetMTFData + 1)) / offset2 * i, 0, i);
                            SetValue(this.rsiBands.GetValue(offsetMTFData, 1) - (this.rsiBands.GetValue(offsetMTFData, 1) - this.rsiBands.GetValue(offsetMTFData + 1, 1)) / offset2 * i, 1, i);
                        }
                    }
                } else {
                    int offset = (int)this.HistoricalData.GetIndexByTime(this.Time().Ticks);
                    int offset1 = (int)this.HistoricalData.GetIndexByTime((this.mtfData[offsetMTFData] as HistoryItemBar).TimeLeft.Ticks);
                    int offset2 = (int)this.HistoricalData.GetIndexByTime((this.mtfData[offsetMTFData + 1] as HistoryItemBar).TimeLeft.Ticks);
                    SetValue(this.rsiBands.GetValue(offsetMTFData) + (this.rsiBands.GetValue(offsetMTFData - 1) - this.rsiBands.GetValue(offsetMTFData)) / (offset2 - offset1) * (offset1 - offset));
                    SetValue(this.rsiBands.GetValue(offsetMTFData, 1) + (this.rsiBands.GetValue(offsetMTFData - 1, 1) - this.rsiBands.GetValue(offsetMTFData, 1)) / (offset2 - offset1) * (offset1 - offset), 1);
                }
            }
        }

        private double BuiltInRSIequivalent(int TargetRSILevel, double P, double N, double PrevRSIBand, double Close, double PrevClose) {
            this.W = 0;
            this.S = 0;

            this.diff = Close - PrevClose;

            if (this.diff > 0)
                this.W = this.diff;
            else if (this.diff < 0)
                this.S = -this.diff;

            if (PrevRSIBand > PrevClose)
                this.HypotheticalCloseToMatchRSITarget = PrevClose + P - P * this.rsiPeriod - ((N * this.rsiPeriod) - N) * TargetRSILevel / (TargetRSILevel - 100);
            else
                this.HypotheticalCloseToMatchRSITarget = PrevClose - N - P + N * this.rsiPeriod + P * this.rsiPeriod + (100 * P) / TargetRSILevel - (100 * P * this.rsiPeriod) / TargetRSILevel;

            if (PrevRSIBand == GetValue(1)) {
                this.PUp = ((this.rsiPeriod - 1) * P + this.W) / this.rsiPeriod;
                this.NUp = ((this.rsiPeriod - 1) * N + this.S) / this.rsiPeriod;
            } else if (PrevRSIBand == GetValue(1, 1)) {
                this.PLow = ((this.rsiPeriod - 1) * P + this.W) / this.rsiPeriod;
                this.NLow = ((this.rsiPeriod - 1) * N + this.S) / this.rsiPeriod;
            }

            return HypotheticalCloseToMatchRSITarget;
        }

        /*public override void OnPaintChart(PaintChartEventArgs args)
        {
            Graphics gr = args.Graphics;
            Font f = new Font("Arial", 10);

            // Draw results for main symbol            
            gr.DrawString("Periode: " + this.HistoricalData.Period, new Font(f, FontStyle.Underline), Brushes.Green, 10, 100);
            gr.DrawString("(0): " + (this.HistoricalData[0] as HistoryItemBar).TimeLeft, f, Brushes.Green, 10, 120);
            gr.DrawString("(1): " + (this.HistoricalData[1] as HistoryItemBar).TimeLeft, f, Brushes.Green, 10, 140);
            gr.DrawString("GetValue(0): " + GetValue(0), f, Brushes.Green, 10, 160);
            gr.DrawString("GetValue(1): " + GetValue(1), f, Brushes.Green, 10, 180);

            if (this.mtfData != null) {
                int offsetMTFData = (int)this.mtfData.GetIndexByTime(this.Time().Ticks);
                gr.DrawString("Periode: " + this.mtfData.Period, new Font(f, FontStyle.Underline), Brushes.LightCoral, 300, 100);
                gr.DrawString("offsetMTFData(" + offsetMTFData + "): " + (this.mtfData[offsetMTFData] as HistoryItemBar).TimeLeft, f, Brushes.LightCoral, 300, 120);
                gr.DrawString("offsetMTFData + 1 (" + (offsetMTFData + 1) + "): " + (this.mtfData[offsetMTFData + 1] as HistoryItemBar).TimeLeft, f, Brushes.LightCoral, 300, 140);
                gr.DrawString("GetValue("+ offsetMTFData + "): " + this.rsiBands.GetValue(offsetMTFData), f, Brushes.LightCoral, 300, 160);
                gr.DrawString("GetValue("+ (offsetMTFData + 1) + "): " + this.rsiBands.GetValue(offsetMTFData + 1), f, Brushes.LightCoral, 300, 180);
            }
        }*/

        protected override void OnClear() {
            this.mtfData?.Dispose();
        }
    }
}
