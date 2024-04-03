using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Abstractions;
using TradingPlatform.BusinessLayer.History.Aggregations;

namespace VWAP {
	public class VWAP : Indicator, IWatchlistIndicator {

        #region Consts

        private const string USE_CUSTOM_OPEN_SESSION_TIME_SI = "Use Custom Start time";
        private const string CUSTOM_OPEN_SESSION_TIME_SI = "Custom Start time";
        private const string RESET_PERIOD_NAME_SI = "ResetPeriod";

        private const string RESET_TYPE_NAME_SI = "Reset type";
        private const string BY_PERIOD_SESSION_TYPE = "By period";
        private const string FULL_HISTORY_SESSION_TYPE = "Full range";

        #endregion Consts

        #region Parameters

        [InputParameter(RESET_TYPE_NAME_SI, 30, variants: [
            BY_PERIOD_SESSION_TYPE, VwapSessionMode.ByPeriod,
            FULL_HISTORY_SESSION_TYPE, VwapSessionMode.FullHistory,
        ])]
        public VwapSessionMode SessionMode;

        public Period ResetPeriod { get; private set; }
        public DateTime CustomStartTime { get; private set; }
        public bool UseCustomStartTime { get; private set; }

        [InputParameter("Sources prices", 1, variants: [
            "Close", VwapPriceType.Close,
            "Open", VwapPriceType.Open,
            "High", VwapPriceType.High,
            "Low", VwapPriceType.Low,
            "HL Avg", VwapPriceType.HL2,
            "HLC Avg", VwapPriceType.HLC3,
            "OHLC Avg", VwapPriceType.OHLC4
        ])]
        public VwapPriceType SourcePrice = VwapPriceType.HLC3;

        [InputParameter("Std Calculation", 2, variants: [
            "Standard Deviation", VwapStdCalculationType.StandardDeviation,
            "VWAPVariance", VwapStdCalculationType.VWAPVariance
        ])]
        public VwapStdCalculationType CalculationType = VwapStdCalculationType.StandardDeviation;

        [InputParameter("Band 1 / Std Deviation Multiplier", 3, 0, 9999, 1, 0)]
        public int bd1StdDevMult = 1;
        [InputParameter("Band 2 / Std Deviation Multiplier", 4, 0, 9999, 1, 0)]
        public int bd2StdDevMult = 2;
        [InputParameter("Band 3 / Std Deviation Multiplier", 5, 0, 9999, 1, 0)]
        public int bd3StdDevMult = 3;
        [InputParameter("Band 4 / Std Deviation Multiplier", 6, 0, 9999, 1, 0)]
        public int bd4StdDevMult = 4;

        public int MinHistoryDepths => 1;

        private HistoryAggregationVwapParameters parameters = null;
        private HistoryRequestParameters historyParameters = null;
        private HistoricalData vwapHistoricalData = null;

        public override string ShortName => $"VWAP ({((this.SessionMode == VwapSessionMode.FullHistory) ? "Full range" : this.GetStepPeriod())})";
        public override string SourceCodeLink => "https://github.com/pcortellezzi/Quantower-Devs/blob/main/Indicators/VWAP/VWAP.cs";
        #endregion Parameters

        public VWAP() : base() {
            this.Name = "VWAP";
            this.Description = "VWAP";

            AddLineSeries("VWAP", Color.CadetBlue, 1, LineStyle.Solid);
            AddLineSeries("StdUp1", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdDown1", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdUp2", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdDown2", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdUp3", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdDown3", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdUp4", Color.CadetBlue, 1, LineStyle.Dash);
            AddLineSeries("StdDown4", Color.CadetBlue, 1, LineStyle.Dash);

            this.SessionMode = VwapSessionMode.ByPeriod;
            this.ResetPeriod = Period.DAY1;
            this.CustomStartTime = DateTime.Now.Date + new TimeSpan(0,0,0);

            this.AllowFitAuto = true;
            this.SeparateWindow = false;
            UpdateType = IndicatorUpdateType.OnTick;
            IsUpdateTypesSupported = false;
        }

        #region Overrides

        protected override void OnInit() {
            this.parameters = new HistoryAggregationVwapParameters() {
                Aggregation = new HistoryAggregationTime(this.HistoricalData.Period),
                DataType = VwapDataType.CurrentTF,
                PriceType = this.SourcePrice,
                StdCalculationType = this.CalculationType,
                TimeZone = this.GetTimeZone(),
            };
            this.historyParameters = new HistoryRequestParameters() {
                HistoryType = this.Symbol.HistoryType,
            };

            switch (this.SessionMode) {
                case VwapSessionMode.FullHistory:
                    this.historyParameters.FromTime = this.HistoricalData.FromTime;
                    break;
                case VwapSessionMode.ByPeriod:
                    this.parameters.Period = this.GetStepPeriod();
                    this.historyParameters.FromTime = this.historyParameters.ToTime - Math.Ceiling((this.HistoricalData.ToTime - this.HistoricalData.FromTime)/this.GetStepPeriod().Duration) * this.GetStepPeriod().Duration;
                    if (this.UseCustomStartTime) {
                        this.historyParameters.SessionsContainer = new CustomSessionsContainer("CustomSession", this.GetTimeZone(), [
                            this.CreateCustomSession(Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(this.CustomStartTime,this.GetTimeZone()).TimeOfDay,
                                                     Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(this.CustomStartTime,this.GetTimeZone()).TimeOfDay,
                                                     this.GetTimeZone().TimeZoneInfo)
                        ]);
                    }
                    break;
            }

            this.historyParameters.Aggregation = new HistoryAggregationVwap(this.parameters);
            this.vwapHistoricalData = this.Symbol.GetHistory(this.historyParameters);

            base.OnInit();
        }

        protected override void OnUpdate(UpdateArgs args) {
            if (this.Count < 1)
                return;

            if (this.vwapHistoricalData[(int)this.vwapHistoricalData.GetIndexByTime(this.Time().Ticks)] is not IVwapHistoryItem vwap)
                return;
            
            SetValue(vwap.Value);
            SetValue(vwap.Value + this.bd1StdDevMult * vwap.STDCoefficient, 1);
            SetValue(vwap.Value - this.bd1StdDevMult * vwap.STDCoefficient, 2);
            SetValue(vwap.Value + this.bd2StdDevMult * vwap.STDCoefficient, 3);
            SetValue(vwap.Value - this.bd2StdDevMult * vwap.STDCoefficient, 4);
            SetValue(vwap.Value + this.bd3StdDevMult * vwap.STDCoefficient, 5);
            SetValue(vwap.Value - this.bd3StdDevMult * vwap.STDCoefficient, 6);
            SetValue(vwap.Value + this.bd4StdDevMult * vwap.STDCoefficient, 7);
            SetValue(vwap.Value - this.bd4StdDevMult * vwap.STDCoefficient, 8);
        }

        public override IList<SettingItem> Settings {
            get {
                var settings = base.Settings;

                var separ = settings.FirstOrDefault()?.SeparatorGroup;

                settings.Add(new SettingItemPeriod(RESET_PERIOD_NAME_SI, this.ResetPeriod, 30) {
                    Text = loc._("Period"),
                    ExcludedPeriods = [BasePeriod.Tick],
                    SeparatorGroup = separ,
                    Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)VwapSessionMode.ByPeriod))
                });

                settings.Add(new SettingItemDateTime(CUSTOM_OPEN_SESSION_TIME_SI, this.CustomStartTime, 30) {
                    Format = DatePickerFormat.Time,
                    SeparatorGroup = separ,
                    Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)VwapSessionMode.ByPeriod)),
                });
                
                settings.Add(new SettingItemBoolean(USE_CUSTOM_OPEN_SESSION_TIME_SI, this.UseCustomStartTime, 30) {
                    SeparatorGroup = separ,
                    Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)VwapSessionMode.ByPeriod)),
                });

                return settings;
            }
            set {
                var holder = new SettingsHolder(value);
                base.Settings = value;

                var needRefresh = false;

                if (holder.TryGetValue(RESET_PERIOD_NAME_SI, out var item)) {
                    var newValue = item.GetValue<Period>();

                    if (this.ResetPeriod != newValue) {
                        this.ResetPeriod = newValue;
                        needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                    }
                }
                if (holder.TryGetValue(USE_CUSTOM_OPEN_SESSION_TIME_SI, out item)) {
                    var newValue = item.GetValue<Boolean>();

                    if (this.UseCustomStartTime != newValue) {
                        this.UseCustomStartTime = newValue;
                        needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                    }
                }
                if (holder.TryGetValue(CUSTOM_OPEN_SESSION_TIME_SI, out item)) {
                    var newValue = item.GetValue<DateTime>();

                    if (this.CustomStartTime != newValue) {
                        this.CustomStartTime = newValue;
                        needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                    }
                }

                if (needRefresh)
                    this.Refresh();
            }
        }

        #endregion Overrides

        #region Nested

        private TradingPlatform.BusinessLayer.TimeZone GetTimeZone() {
            return this.CurrentChart?.CurrentTimeZone ?? Core.Instance.TimeUtils.SelectedTimeZone;
        }
        private Period GetStepPeriod() {
            if (this.SessionMode == VwapSessionMode.FullHistory)
                return Period.DAY1;
            else
                return this.ResetPeriod;
        }

        private CustomSession CreateCustomSession(TimeSpan open, TimeSpan close, TimeZoneInfo info) {
            var session = new CustomSession {
                OpenOffset = open,
                CloseOffset = close,
                IsActive = true,
                Name = "Main",
                Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToArray(),
                Type = SessionType.Main
            };
            session.RecalculateOpenCloseTime(info);
            return session;
        }

        public enum VwapSessionMode {
            ByPeriod,
            FullHistory,
        }

        #endregion Nested
    }
}


