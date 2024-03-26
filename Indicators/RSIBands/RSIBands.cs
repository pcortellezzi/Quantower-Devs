using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace RSIBands {
	public class RSIBands : Indicator, IWatchlistIndicator {
        private HistoricalDataCustom hdc = null;
        private Indicator EMAu = null;
        private Indicator EMAd = null;

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

        public int MinHistoryDepths => 1000;

        public override string ShortName => $"{this.Name} ({this.rsiPeriod}: {this.RSIUpperLevel}/{this.RSILowerLevel} @ {this.MTFPeriod})";
        public override string SourceCodeLink => "https://github.com/pcortellezzi/Quantower-Devs/blob/main/Indicators/RSIBands/RSIBands.cs";

        public RSIBands() : base() {
            Name = "RSIBands";
            Description = @"Francois Bertrand's RSI Bands as outlined in Stocks & Commodities April 2008 issue.";

            AddLineSeries("RSIBandUpper", Color.CadetBlue, 1, LineStyle.Solid);
            AddLineSeries("RSIBandMiddle", Color.WhiteSmoke, 1, LineStyle.Dash);
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
            } else {
                this.EMAu = Core.Indicators.BuiltIn.EMA(2 * this.rsiPeriod - 1, PriceType.Open);
                this.EMAd = Core.Indicators.BuiltIn.EMA(2 * this.rsiPeriod - 1, PriceType.Close);
                this.hdc = new HistoricalDataCustom(this);
                this.hdc.AddIndicator(this.EMAu);
                this.hdc.AddIndicator(this.EMAd);
            }
        }

        protected override void OnUpdate(UpdateArgs args) {

            if (this.Count < 1)
                return;

            if (this.mtfData == null) {
                this.hdc[PriceType.Open, 0] = Math.Max(Close() - Close(1), 0);
                this.hdc[PriceType.Close, 0] = Math.Max(Close(1) - Close(), 0);
                double xu = (this.rsiPeriod - 1) * (this.EMAd.GetValue() * this.RSIUpperLevel / (100 - this.RSIUpperLevel) - this.EMAu.GetValue());
                double xl = (this.rsiPeriod - 1) * (this.EMAd.GetValue() * this.RSILowerLevel / (100 - this.RSILowerLevel) - this.EMAu.GetValue());
                double rsiUpper = (xu >= 0 ? Close() + xu : Close() + xu * (100 - this.RSIUpperLevel) / this.RSIUpperLevel);
                double rsiLower = (xl >= 0 ? Close() + xl : Close() + xl * (100 - this.RSILowerLevel) / this.RSILowerLevel);
                SetValue(rsiUpper);
                SetValue(rsiLower + (rsiUpper - rsiLower) / 2, 1);
                SetValue(rsiLower, 2);
            }
            else {
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

        protected override void OnClear() {
            this.mtfData?.Dispose();
            this.mtfData = null;
        }
    }
}
