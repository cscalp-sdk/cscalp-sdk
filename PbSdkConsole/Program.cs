using ICore;
using PbSdk;
using PbSdkConsole;
using System.Threading;

IPbSdkManager _sdk;

string _findStockLongName = "";//необходимо задать;//пример для поиска сервера по названию;
string _account = "";//необходимо задать;
string _password = "";//необходимо задать;
DispatcherSlotEventsReceiver _slotEvRec = null;

//string _tickerSymbol = "GAZP";
string _tickerSecurityID = "TQBR.GAZP";//более уникальный идентификатор инструмента (на разных секциях подключения точно не повторится);
LayerEventsReceiver _layerEvRec = null;

try
{
    Console.WriteLine("Creating...");
    _sdk = PbSdkFactory.Create();
    Console.WriteLine("Created");

    Console.WriteLine("Authorization...");
    if (!await _sdk.AuthAsync())
    {
        throw new Exception("Authorization failed");
    }
    Console.WriteLine("Authorized");

    Console.WriteLine("Starting...");
    _sdk.Initialize();
    await _sdk.StartAsync();
    Console.WriteLine("Started");

    DO_SOMETHING();//>>> ПРИМЕР ПОДКЛЮЧЕНИЯ К ПБ;

    Console.WriteLine("Press any key to stop...");//*;
    Console.ReadKey();

    Console.WriteLine("Stopping...");
    await _sdk.StopAsync();//сохранит ранее добавленные слоты диспетчера и свойства;
    Console.WriteLine("Stopped");
    Console.WriteLine("Press any key to exit...");//*;
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex}");
    Console.ReadKey();
}

void DO_SOMETHING()
{
    string findStockLongName = _findStockLongName;//пример для поиска сервера по названию;
    string account = _account;//необходимо задать;
    string password = _password;//необходимо задать;
    if (string.IsNullOrWhiteSpace(findStockLongName)
        || string.IsNullOrWhiteSpace(account)
        || string.IsNullOrWhiteSpace(password))
    { throw new Exception("Not enough data for test connection"); }

    //поиск или добавление слота подключения (если не было);
    IDispatcher dispatcher = _sdk.PrivodBondarDispatcher;
    IDispatcherSlot slot = null;
    if (dispatcher.Slots.Count == 0)
    {
        dispatcher.TryAddSlot(DispatcherSlotType.XDealingCommon, out slot, out _);
        //dispatcher.TryGetSlotType(slot, out DispatcherSlotType slotType);
    }
    else
    {
        slot = dispatcher.GetSlot(0);
    }

    if (slot != null)
    {
        //добавляем получателя событий слота;
        _slotEvRec = new DispatcherSlotEventsReceiver(slot);
        _slotEvRec.Log += _slotEvRec_Log;
        _slotEvRec.CanCreateLayers += _slotEvRec_CanCreateLayers;

        //поиск эндпоинта для подключения (для примера - по LongName);
        //задание ему логина-пароля;
        //подключение;
        //> все события будут приходить в получателя событий и выдаваться в консоль
        var si = slot.PublicServersInfo.FirstOrDefault(a => a.StockLongName == findStockLongName);
        if (si != null)
        {
            slot.PublicServerInfo = si;

            slot.Account = account;
            slot.Password = password;

            slot.ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}

void _slotEvRec_Log(string message)
{
    Console.WriteLine(message);
}

void _slotEvRec_CanCreateLayers(DispatcherSlotEventsReceiver sender)
{
    //поиск инструмента:

    IContractInfo tickerInfo = sender.Slot.ContractsTable.SearchContractByID(_tickerSecurityID);
    //info требуется для создания слоя инструмента;

    //создание слоя инструмента:

    if (tickerInfo == null)
    { Console.WriteLine($"ERROR: Ticker not found ({_tickerSecurityID})"); }
    else
    {
        var layerData = sender.Slot.InvokeLayerAsync(tickerInfo, CancellationToken.None).GetAwaiter().GetResult();
        //во время подключение создается только один ILayer на IContractInfo.
        //если уже был, то вернет уже созданный (ILayer, false);

        ILayer layer = layerData.layer;
        if (layer != null)
        {
            _layerEvRec = new LayerEventsReceiver(layer);
            _layerEvRec.Log += _layerEvRec_Log;

            if (layerData.isnew)
            {
                sender.Slot.InvokeLayerEndAsync(sender, layer, CancellationToken.None).GetAwaiter().GetResult();
                //вызывать End обязательно - отправит репликации (текущее состояние портфеля по инструменту) подписчикам;
            }

            Task.Run(RunLayerTradingSample_1);
        }
    }
}

void _layerEvRec_Log(string message)
{
    Console.WriteLine(message);
}

//пример совершения торговых операций;
async Task RunLayerTradingSample_1()
{
    //при появлении лонговой позиции выставить заявку на продажу и выставить стоп-лосс;

    string logHeader = "LayerTradingSample_1";
    Console.WriteLine($"{logHeader}");

    bool waiting = true;
    while (waiting)
    {
        await Task.Delay(1000);

        //базовый набор проверки для "разрешено торговать;
        //- слот - подключен, получена таблица инструментов, получены и выданы репликации;
        //- слой - получил и выдал репликации, получил стакан;
        ILayer layer = _layerEvRec?.Layer;
        if (layer != null
            //
            && layer.DispatcherSlot.IsReady
            //
            && !layer.IsReplications
            && layer.IsReplicationsDone
            //
            && layer.IsStockReceived
            && IsStockReady(layer.LastStock))
        {
            Console.WriteLine($"{logHeader}: Waiting for PositionAmount > 0");
            if (layer.Posmonitor._PositionAmount > 0)
            {
                waiting = false;//break;

                Console.WriteLine($"{logHeader}: ACTION");

                //биржевая заявка:

                double orderPrice = layer.Posmonitor._PositionPrice + (layer.ContractInfo.Step * 30);//на N шагов цены инструмента;
                orderPrice = Math.Round(orderPrice, layer.ContractInfo.Decimals);//округление до количества знаков запятой этого инструмента;

                decimal orderAmount = Math.Abs(layer.Posmonitor._PositionAmount);//короткая позиция была бы меньше ноля;

                bool orderSuccess = layer.SendOrder(orderPrice, orderAmount, OrderAction.Sell, type: OrderFlags.Limit);
                //layer.IsServerMarketOrdersAvailable -> можно OrderFlags.Market;
                //OrderFlags.Limit доступно всегда;

                //стоп-лосс:

                double slPrice = layer.Posmonitor._PositionPrice - (layer.ContractInfo.Step * 30);
                slPrice = Math.Round(slPrice, layer.ContractInfo.Decimals);

                bool slSuccess = layer.SendStopOrder(
                     stop_price: slPrice,
                     price: 0,//цена биржевой заявки при срабатывании стоп-заявки, для bymarket==true необязательно;
                     amount: 0m,//для стоп-лосса не обязательно;
                     direction: OrderAction.Sell,
                     type: StopOrderType.StopLoss,
                     method: StopOrderMethod.TickPrice,
                     location: StopOrderLocation.Server,
                     bymarket: true);
                //IDispatcherSlot.CheckFeature(DispatcherSlotFeature.StopOrdersAvailable);
                //StopOrderMethod -> IDispatcherSlot.AvailableStopOrdersMethods;
                //bymarket -> ILayer.IsServerMarketStopOrdersAvailable;

                //вызов отправки заявки через слой:
                //- дожидается ответа от сервера, а не только отправки команды по сети;
                //- возвращает bool в зависимости от результата выставления заявки;

                //* отмена заявок:
                //
                //layer.CancelOrder(id, wait_cancel:true);
                //layer.CancelStopOrder(id, wait_cancel:true);
                //
                //список заявок по которому можно получить ID заявки для отмены доступен в
                //layer.Lomonitor._List (биржевые);
                //layer.Somonitor._List (стоп-заявки);
                //рекомендуется подробнее изучить класс LayerEventsReceiver;
            }
        }
    }

    Console.WriteLine($"{logHeader}: End");
}

bool IsStockReady(ContractOrderStock stock)
{
    if (stock != null
        && stock.BestAsk != 0D && stock.BestBid != 0D
        && stock.BestAsk >= stock.BestBid)
    { return true; }
    return false;
}