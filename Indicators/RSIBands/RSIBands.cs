using System;
using System.Drawing;
using System.Linq;
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

        private HistoricalData mtfData = null;
        private Indicator mtfIndicator = null;

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
            Name = "RSIBands";
            Description = @"Francois Bertrand's RSI Bands as outlined in Stocks & Commodities April 2008 issue.";

            AddLineSeries("RSIBandUpper", Color.CadetBlue, 1, LineStyle.Solid);
            AddLineSeries("RSIBandLower", Color.CadetBlue, 1, LineStyle.Solid);

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
            if (MTFPeriod != this.HistoricalData.Period){
                this.mtfData = this.Symbol.GetHistory(MTFPeriod, this.HistoricalData.HistoryType, this.HistoricalData.FromTime);
                this.mtfIndicator = new RSIBands(this.MTFPeriod, this.rsiPeriod, this.RSIUpperLevel, this.RSILowerLevel);
                this.mtfData.AddIndicator(this.mtfIndicator);
            }
        }

        protected override void OnUpdate(UpdateArgs args) {

            if (this.Count < 1)
                return;

            if (this.mtfData == null) {
                //indicator calculation
                if (args.Reason != UpdateReason.NewTick) {
                    this.PUpOld = this.PUp;
                    this.NUpOld = this.NUp;
                    this.PLowOld = this.PLow;
                    this.NLowOld = this.NLow;
                }

                SetValue(BuiltInRSIequivalent(this.RSIUpperLevel, this.PUpOld, this.NUpOld, GetValue(1), this.Close(0), this.Close(1)));
                SetValue(BuiltInRSIequivalent(this.RSILowerLevel, this.PLowOld, this.NLowOld, GetValue(1, 1), this.Close(0), this.Close(1)), 1);
            } else {
                //generic MTF calculation
                int mtfOffset = (int)this.mtfData.GetIndexByTime(this.Time().Ticks);
                if ((this.mtfData[mtfOffset] as HistoryItemBar).TimeLeft == this.Time()) {
                    int prevOffset = (int)this.HistoricalData.GetIndexByTime((this.mtfData[mtfOffset + 1] as HistoryItemBar).TimeLeft.Ticks);
                    int curOffset = (int)this.HistoricalData.GetIndexByTime(this.Time().Ticks);
                    for (int i = 0; i < this.LinesSeries.Count(); i++) {
                        double prevValue = this.mtfIndicator.GetValue(mtfOffset + 1, i);
                        double curValue = this.mtfIndicator.GetValue(mtfOffset, i);
                        for (int j = 1; j <= (prevOffset - curOffset); j++) {
                            SetValue(prevValue + (curValue - prevValue) / (prevOffset - curOffset) * j, i, (prevOffset - curOffset) - j);
                        }
                    }
                } else {
                    for (int i = 0; i < this.LinesSeries.Count(); i++) {
                        SetValue(this.mtfIndicator.GetValue(mtfOffset, i), i);
                    }
                }
            }

        }

        private double BuiltInRSIequivalent(int TargetRSILevel, double P, double N, double PrevRSIBand, double Close, double PrevClose) {
            double W = 0;
            double S = 0;
            double diff = Close - PrevClose;
            double HypotheticalCloseToMatchRSITarget = 0;

            if (diff > 0)
                W = diff;
            else if (diff < 0)
                S = -diff;

            if (PrevRSIBand > PrevClose)
                HypotheticalCloseToMatchRSITarget = PrevClose + P - P * this.rsiPeriod - ((N * this.rsiPeriod) - N) * TargetRSILevel / (TargetRSILevel - 100);
            else
                HypotheticalCloseToMatchRSITarget = PrevClose - N - P + N * this.rsiPeriod + P * this.rsiPeriod + (100 * P) / TargetRSILevel - (100 * P * this.rsiPeriod) / TargetRSILevel;

            if (PrevRSIBand == GetValue(1)) {
                this.PUp = ((this.rsiPeriod - 1) * P + W) / this.rsiPeriod;
                this.NUp = ((this.rsiPeriod - 1) * N + S) / this.rsiPeriod;
            } else if (PrevRSIBand == GetValue(1, 1)) {
                this.PLow = ((this.rsiPeriod - 1) * P + W) / this.rsiPeriod;
                this.NLow = ((this.rsiPeriod - 1) * N + S) / this.rsiPeriod;
            }

            return HypotheticalCloseToMatchRSITarget;
        }

        protected override void OnClear() {
            this.mtfData?.Dispose();
            this.mtfData = null;
        }
    }
}
