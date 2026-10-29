using ICore;
using System.Collections.ObjectModel;

namespace PbSdkConsole
{
    /// <summary>
    /// 
    /// </summary>
    internal class LayerEventsReceiver : ILayerEventsReceiver
    {

        /// <summary>
        /// 
        /// </summary>
        public LayerEventsReceiver(ILayer layer)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(_layer));

            //для получения событий от слоя инструмента;
            _layer.AddLayerEventsReceiverAsync(this, CancellationToken.None).GetAwaiter().GetResult();
        }

        #region [Fields/Properties]

        readonly ILayer _layer;
        public ILayer Layer => _layer;

        public event Action<string> Log;

        readonly LayerEventsReceiverSubscribtions _layerSubscriptions = LayerEventsReceiverSubscribtions.Assets | LayerEventsReceiverSubscribtions.OrderStates |
            LayerEventsReceiverSubscribtions.OrderStock | LayerEventsReceiverSubscribtions.Position |
            LayerEventsReceiverSubscribtions.Replications | LayerEventsReceiverSubscribtions.Trades |
            LayerEventsReceiverSubscribtions.Ticks;//all

        ContractOrderStock _lastStock;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        void OnLog(string message)
        {
            Log?.Invoke($"Layer ({_layer.ContractInfo.Symbol}): {message}");
        }

        #region [ILayerEventsReceiver]

        /// <inheritdoc cref="ILayerEventsReceiver.LayerSubscriptions"/>
        public LayerEventsReceiverSubscribtions LayerSubscriptions => _layerSubscriptions;

        /// <inheritdoc cref="ILayerEventsReceiver.OnLayerDestroyedAsync"/>
        public Task OnLayerDestroyedAsync(ILayer sender, CancellationToken cancellationToken)
        {
            OnLog("OnLayerDestroyed");
            //при отключении все слои инструментов уничтожаются и не переиспользуемы;
            return Task.CompletedTask;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnReplicationBegin"/>
        public void OnReplicationBegin(ILayer sender)
        {
            OnLog("OnReplicationBegin");
            //ILayer.IsReplications == true;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnReplicationEnd"/>
        public void OnReplicationEnd(ILayer sender)
        {
            OnLog("OnReplicationEnd");
            //получено актуальное состояние портфеля по инструменту, можно торговать;
            //ILayer.IsReplicationsDone == true;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnTrade"/>
        public void OnTrade(ILayer sender, TradeData data)
        {
            OnLog($"OnTrade: Price={data.Price}; Amount={data.Amount}; TradeID={data.ID}");
            //ILayer.Posmonitor._Trades;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnClosingTrade"/>
        public void OnClosingTrade(ILayer sender, ClosingTradeData data)
        {
            OnLog($"OnClosingTrade");
            //частичное закрытие позиции;
            //ILayer.Posmonitor._ClosingTrades;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnTradesPosition"/>
        public void OnTradesPosition(ILayer sender, ITradesPositionData data)
        {
            //not in use;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnPositionUpdate"/>
        public void OnPositionUpdate(ILayer sender, decimal amount, double price)
        {
            OnLog($"OnPositionUpdate: Price={price}; Amount={amount}");
            //ILayer.Posmonitor._AveragingMethod = RecalcProfitState.Dispatcher;
            //ILayer.Posmonitor._PositionAmount;
            //ILayer.Posmonitor._PositionPrice;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnLimitOrdersChanged"/>
        public void OnLimitOrdersChanged(ILayer sender, ReadOnlyCollection<LimitOrder> list)
        {
            OnLog($"OnLimitOrdersChanged: Count={list.Count}");
            //ILayer.Lomonitor._List;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnLimitOrderOpFailed"/>
        public void OnLimitOrderOpFailed(ILayer sender, int code, string msg)
        {
            OnLog($"OnLimitOrderOpFailed: Code={code}; Message={msg}");
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnLimitOrderPingReceived"/>
        public void OnLimitOrderPingReceived(ILayer sender, long ms)
        {

        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnStopOrdersChanged"/>
        public void OnStopOrdersChanged(ILayer sender, ReadOnlyCollection<StopOrder> list)
        {
            OnLog($"OnStopOrdersChanged: Count={list.Count}");
            //ILayer.Somonitor._List;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnStopOrderOpFailed"/>
        public void OnStopOrderOpFailed(ILayer sender, int code, string msg)
        {
            OnLog($"OnStopOrderOpFailed: Code={code}; Message={msg}");
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnStopOrderPingReceived"/>
        public void OnStopOrderPingReceived(ILayer sender, long ms)
        {

        }

        #region [ILayerEventsReceiver.MarketData]

        /// <inheritdoc cref="ILayerEventsReceiver.OnStockUpdate"/>
        public void OnStockUpdate(ILayer sender, ContractOrderStock stock)
        {
            _lastStock = stock;
            //ILayer.IsStockReceived == true;
            //ILayer.LastStock;

            //ВАЖНО;
            //если необходимо обрабатывать массив цен в цикле,
            //то необходима следующая конструкция;
            lock (stock)
            {
                if (!stock.IsDisposed)
                {
                    //тогда можно;
                    //foreach (var row in stock.Asks)//stock.Bids;
                    //{
                    //    var price = row.Price;
                    //    var amount = row.Amount;
                    //}
                }
            }
            //но .BestAsk .BestBid доступны всегда;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnTick(ILayer, TickGraphicData)"/>
        public void OnTick(ILayer sender, TickGraphicData tick)
        {
            //в рамках одного подключения действует только один канал получения тиков из двух (одиночный или массивом);
            //ILayer.LastTick;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnTick(ILayer, List{TickGraphicData})"/>
        public void OnTick(ILayer sender, List<TickGraphicData> ticks)
        {
            //в рамках одного подключения действует только один канал получения тиков из двух (одиночный или массивом);
            //ILayer.LastTick;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnTick_DarkPool"/>
        public void OnTick_DarkPool(ILayer sender, TickGraphicData tick)
        {

        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnWatchlistUpdate"/>
        public void OnWatchlistUpdate(ILayer sender, IWatchlist data)
        {

        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnCandlesReceived"/>
        public void OnCandlesReceived(ILayer sender, CandleTimeframe timeframe, List<BaseTicksCandle> candles)
        {
            //требуется запрос, не на всех подключениях может быть доступно;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnClustersReceived"/>
        public void OnClustersReceived(ILayer sender, CandleTimeframe timeframe, List<TickGraphicData> clusters)
        {
            //требуется запрос, не на всех подключениях может быть доступно;
        }

        /// <inheritdoc cref="ILayerEventsReceiver.OnContractExpiredAsync"/>
        public Task OnContractExpiredAsync(ILayer sender, IContractInfo current, IContractInfo next)
        {
            return Task.CompletedTask;
        }

        #endregion

        #endregion
    }
}
