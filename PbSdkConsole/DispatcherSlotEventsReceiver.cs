using ICore;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PbSdkConsole
{
    /// <summary>
    /// 
    /// </summary>
    internal class DispatcherSlotEventsReceiver : IDispatcherSlotEventsReceiver
    {

        /// <summary>
        /// 
        /// </summary>
        public DispatcherSlotEventsReceiver(IDispatcherSlot slot)
        {
            _slot = slot ?? throw new ArgumentNullException(nameof(_slot));

            //для получения событий от слота;
            _slot.AddDispatcherSlotEventsReceiver(this);
        }

        #region [Fields/Properties]

        readonly IDispatcherSlot _slot;
        public IDispatcherSlot Slot => _slot;

        public event Action<string> Log;

        public event Action<DispatcherSlotEventsReceiver> CanCreateLayers;

        readonly Dictionary<object, FinDataViewModel> _updateModelsDictionary = new Dictionary<object, FinDataViewModel>();
        readonly List<FinDataViewModel> _updateModelsCollection = new List<FinDataViewModel>();

        #endregion

        /// <summary>
        /// 
        /// </summary>
        void OnLog(string message)
        {
            Log?.Invoke($"Slot: {message}");
        }

        /// <summary>
        /// 
        /// </summary>
        void LogCurrentFinData()
        {
            foreach (var item in _updateModelsCollection)
            {
                var logParts = new List<string>();

                //текстовые;
                Append(logParts, nameof(item.SubAccount), item.SubAccount);
                Append(logParts, nameof(item.Wallet), item.Wallet);
                Append(logParts, nameof(item.Currency), item.Currency);
                Append(logParts, nameof(item.Notation), item.Notation);

                //значения;
                Append(logParts, nameof(item.TotalAssets), item.TotalAssets?.ToString());
                Append(logParts, nameof(item.FreeAssets), item.FreeAssets?.ToString());
                Append(logParts, nameof(item.BlockedAssets), item.BlockedAssets?.ToString());
                Append(logParts, nameof(item.PoolAssets), item.PoolAssets?.ToString());
                Append(logParts, nameof(item.Commission), item.Commission?.ToString());
                Append(logParts, nameof(item.MarginCall), item.MarginCall?.ToString());
                Append(logParts, nameof(item.PnL), item.PnL?.ToString());
                Append(logParts, nameof(item.Margin), item.Margin?.ToString());

                if (logParts.Count > 0)
                { OnLog(string.Join("; ", logParts)); }
            }

            void Append(List<string> parts, string name, string? value)
            {
                if (!string.IsNullOrEmpty(value))
                { parts.Add($"{name}: {value}"); }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        void ClearFinData()
        {
            _updateModelsDictionary.Clear();
            _updateModelsCollection.Clear();
        }
        /// <summary>
        /// Обновление финансовой информации
        /// </summary>
        void HandleFinDataUpdate(IDispatcherSlot sender, object updateKey, IFinDataUpdate update)
        {
            //* update if schema has flag and value is not null in update;
            //* one key - one model; key is unique "row" identifier;

            //get/add model;

            FinDataViewModel updateModel;//(IFinDataUpdate + set + view properties);

            if (!(_updateModelsDictionary.TryGetValue(updateKey, out updateModel) && updateModel != null))
            {
                updateModel = new FinDataViewModel(slotOwner: sender);

                _updateModelsDictionary.Add(updateKey, updateModel);
                _updateModelsCollection.Add(updateModel);
            }

            //accept update by schema;

            FinDataUpdateType updateSchema = sender.FinDataUpdateSchema;

            #region [IFinDataUpdate]

            if (updateSchema.HasFlag(FinDataUpdateType.SubAccount) && update.SubAccount != null)
            {
                updateModel.SubAccount = update.SubAccount;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.Wallet) && update.Wallet != null)
            {
                updateModel.Wallet = update.Wallet;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.Currency) && update.Currency != null)
            {
                updateModel.Currency = update.Currency;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.TotalAssets) && update.TotalAssets.HasValue)
            {
                updateModel.TotalAssets = update.TotalAssets;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.FreeAssets) && update.FreeAssets.HasValue)
            {
                updateModel.FreeAssets = update.FreeAssets;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.BlockedAssets) && update.BlockedAssets.HasValue)
            {
                updateModel.BlockedAssets = update.BlockedAssets;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.PoolAssets) && update.PoolAssets.HasValue)
            {
                updateModel.PoolAssets = update.PoolAssets;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.Commission) && update.Commission.HasValue)
            {
                updateModel.Commission = update.Commission;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.MarginCall) && update.MarginCall.HasValue)
            {
                updateModel.MarginCall = update.MarginCall;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.PnL) && update.PnL.HasValue)
            {
                updateModel.PnL = update.PnL;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.Margin) && update.Margin.HasValue)
            {
                updateModel.Margin = update.Margin;
            }

            if (updateSchema.HasFlag(FinDataUpdateType.Notation) && update.Notation != null)
            {
                updateModel.Notation = update.Notation;
            }

            #endregion
        }

        #region [IDispatcherSlotEventsReceiver]

        #region [README]

        //Про получение таблицы инструментов:
        //- событие получения таблицы инструментов (OnContractsTableUpdated)
        //приходит сразу после события подключения (OnConnected).
        //- после получения таблицы инструментов можно начинать создавать слои инструментов (ILayer).
        //таблица инструментов состоит из IContractInfo - общей информации про инструмент (название, шаг цены, ...);
        //- нет необходимости сохранять таблицу инструментов, она всегда доступна в IDispatcherSlot.ContractsTable,
        //но только в рамках подключения, иначе null.
        //if (IDispatcherSlot.IsConnected && IDispatcherSlot.IsContractsTableUpdated) -> можно пользоваться ContractsTable;

        //Про получение информации о денежных средствах:
        //- после подключения и получения таблицы инструментов начинаются "репликации",
        //то есть получение событий о текущем состоянии портфеля (деньги, позиции, заявки);
        //- деньги приходят в OnFinDataUpdate;
        //- позиции, совершенные сделки и заявки приходят только по созданным пользователем слоям инструмента (ILayer),
        //после создания слоя - в репликациях (OnLayerReplicationsBegin -> СобытияХ -> OnLayerReplicationsEnd),
        //в процессе работы - СобытияХ,
        //где "СобытияХ" это открытые позиции (OnPositionUpdate), совершенные сделки (OnTrade), открытые заявки (OnLimitOrdersChanged+OnStopOrdersChanged);
        //-* для получения информации в разрезе инструмента правильнее будет создать слой и добавиться к нему в подписчики;

        //Совершать торговые операции можно только после подключения, получения инструментов, завершения репликаций.
        //IDispatcherSlot.IsReady (.IsConnected + .IsContractsTableUpdated + .IsReplicationsDone);

        #endregion

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnConnected"/>
        public void OnConnected(IDispatcherSlot sender)
        {
            OnLog("OnConnected");
            //IDispatcherSlot.IsConnected;
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnConnectionFailed"/>
        public void OnConnectionFailed(IDispatcherSlot sender, string reason)
        {
            OnLog("OnConnectionFailed");
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnDisconnected"/>
        public void OnDisconnected(IDispatcherSlot sender)
        {
            //приходит не только при нормальном (ручном) отключении, но и при неудачном подключении;
            OnLog("OnDisconnected");
            ClearFinData();
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnDisposed"/>
        public void OnDisposed(IDispatcherSlot sender)
        {
            OnLog("OnDisposed");
            ClearFinData();
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnContractsTableUpdatedAsync"/>
        public Task OnContractsTableUpdatedAsync(IDispatcherSlot sender, CancellationToken cancellationToken)
        {
            OnLog("OnContractsTableUpdated");
            //IDispatcherSlot.IsContractsUpdated;
            CanCreateLayers?.Invoke(this);
            return Task.CompletedTask;
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnFinDataUpdate"/>
        public void OnFinDataUpdate(IDispatcherSlot sender, object updateKey, IFinDataUpdate update)
        {
            //- информация в update может приходить частями.
            //например, в одном случае заблокированные средства, в другом свободные (остальные значения null);
            OnLog("OnFinDataUpdate");
            HandleFinDataUpdate(sender, updateKey, update);
            LogCurrentFinData();
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnTickersScreenerUpdate"/>
        public void OnTickersScreenerUpdate(IDispatcherSlot sender, ITickersScreener data, List<IWatchlist> changedTickers = null)
        {
            //IDispatcherSlot.Screener;
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnTopProfiters"/>
        public void OnTopProfiters(IDispatcherSlot sender, object key, List<string> tickers, List<double> percents)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnDiaryGuidReceived"/>
        public void OnDiaryGuidReceived(IDispatcherSlot sender, string guid)
        {

        }

        #region [IDispatcherSlotEventsReceiver - ILayer (информация по созданным слоям инструмента)]
        //см. LayerEventsReceiver;

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnLayerReplicationsBegin"/>
        public void OnLayerReplicationsBegin(IDispatcherSlot sender, ILayer source)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnLayerReplicationsEnd"/>
        public void OnLayerReplicationsEnd(IDispatcherSlot sender, ILayer source)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnPositionUpdate"/>
        public void OnPositionUpdate(IDispatcherSlot sender, ILayer source, double price, decimal amount)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnTrade"/>
        public void OnTrade(IDispatcherSlot sender, ILayer source, TradeData data)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnClosingTrade"/>
        public void OnClosingTrade(IDispatcherSlot sender, ILayer source, ClosingTradeData data)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnTradesPosition"/>
        public void OnTradesPosition(IDispatcherSlot sender, ILayer source, ITradesPositionData data)
        {
            //not in use;
        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnLimitOrdersChanged"/>
        public void OnLimitOrdersChanged(IDispatcherSlot sender, ILayer source, ReadOnlyCollection<LimitOrder> list)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnStopOrdersChanged"/>
        public void OnStopOrdersChanged(IDispatcherSlot sender, ILayer source, ReadOnlyCollection<StopOrder> list)
        {

        }

        /// <inheritdoc cref="IDispatcherSlotEventsReceiver.OnPoolOperationResult"/>
        public void OnPoolOperationResult(IDispatcherSlot sender, bool success, string reason)
        {

        }

        #endregion

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
    internal class FinDataViewModel : IFinDataUpdate, INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public FinDataViewModel(object slotOwner)
        {
            _slotOwner = slotOwner;
        }

        readonly object _slotOwner = null;
        /// <summary>
        /// 
        /// </summary>
        public object SlotOwner => _slotOwner;

        #region [IFinDataUpdate]

        string _subAccount = null;
        /// <summary>
        /// 
        /// </summary>
        public string SubAccount
        {
            get { return _subAccount; }
            set
            {
                if (_subAccount != value)
                {
                    _subAccount = value;
                    OnPropertyChanged(nameof(SubAccount));
                }
            }
        }

        string _wallet = null;
        /// <summary>
        /// 
        /// </summary>
        public string Wallet
        {
            get { return _wallet; }
            set
            {
                if (_wallet != value)
                {
                    _wallet = value;
                    OnPropertyChanged(nameof(Wallet));
                }
            }
        }

        string _currency = null;
        /// <summary>
        /// 
        /// </summary>
        public string Currency
        {
            get { return _currency; }
            set
            {
                if (_currency != value)
                {
                    _currency = value;
                    OnPropertyChanged(nameof(Currency));
                }
            }
        }

        decimal? _totalAssets = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? TotalAssets
        {
            get { return _totalAssets; }
            set
            {
                if (_totalAssets != value)
                {
                    _totalAssets = value;
                    OnPropertyChanged(nameof(TotalAssets));
                }
            }
        }

        decimal? _freeAssets = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? FreeAssets
        {
            get { return _freeAssets; }
            set
            {
                if (_freeAssets != value)
                {
                    _freeAssets = value;
                    OnPropertyChanged(nameof(FreeAssets));
                }
            }
        }

        decimal? _blockedAssets = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? BlockedAssets
        {
            get { return _blockedAssets; }
            set
            {
                if (_blockedAssets != value)
                {
                    _blockedAssets = value;
                    OnPropertyChanged(nameof(BlockedAssets));
                }
            }
        }

        decimal? _poolAssets = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? PoolAssets
        {
            get { return _poolAssets; }
            set
            {
                if (_poolAssets != value)
                {
                    _poolAssets = value;
                    OnPropertyChanged(nameof(PoolAssets));
                }
            }
        }

        decimal? _commission = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? Commission
        {
            get { return _commission; }
            set
            {
                if (_commission != value)
                {
                    _commission = value;
                    OnPropertyChanged(nameof(Commission));
                }
            }
        }

        decimal? _marginCall = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? MarginCall
        {
            get { return _marginCall; }
            set
            {
                if (_marginCall != value)
                {
                    _marginCall = value;
                    OnPropertyChanged(nameof(MarginCall));
                }
            }
        }

        decimal? _pnL = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? PnL
        {
            get { return _pnL; }
            set
            {
                if (_pnL != value)
                {
                    _pnL = value;
                    OnPropertyChanged(nameof(PnL));
                }
            }
        }

        decimal? _margin = null;
        /// <summary>
        /// 
        /// </summary>
        public decimal? Margin
        {
            get { return _margin; }
            set
            {
                if (_margin != value)
                {
                    _margin = value;
                    OnPropertyChanged(nameof(Margin));
                }
            }
        }

        string _notation = null;
        /// <summary>
        /// 
        /// </summary>
        public string Notation
        {
            get { return _notation; }
            set
            {
                if (_notation != value)
                {
                    _notation = value;
                    OnPropertyChanged(nameof(Notation));
                }
            }
        }

        #endregion

        #region [INotifyPropertyChanged]

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region [IDisposable]

        bool _isDisposed = false;

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            { return; }
            _isDisposed = true;
        }

        #endregion
    }
}
