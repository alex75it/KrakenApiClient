﻿module internal parser

[<assembly:System.Runtime.CompilerServices.InternalsVisibleTo("UnitTests")>] do()

open System
open System.Collections.Generic
open FSharp.Data
open Alex75.Cryptocurrencies
open api.response.models


let private load_result_and_check_errors jsonString =
    let json = JsonValue.Parse(jsonString)    
    let errors = json.["error"].AsArray()    
    if errors.Length > 0 then failwith (errors.[0].AsString())
    json.["result"]

let mapBTC currency = if currency = "XBT" then "BTC" else currency

/// Creates a map <Kraken currency>:<currency>
let parseAssets (jsonString) =
    let result = load_result_and_check_errors jsonString
    result.Properties()
    |> Seq.map (fun (name, json) -> (name, json.["altname"].AsString()) )

let parsePairs (content:string) =
    let result = load_result_and_check_errors content

    let pairs = new List<CurrencyPair>()
    for key, record in result.Properties() |> Seq.where( fun (key, record) -> not(key.EndsWith(".d"))) // skip "Derivatives"
        do    

        // must be used with a continuosly updated list
        //let _base = currency_mapping.get_currency (record.["base"].AsString())
        //let quote = currency_mapping.get_currency (record.["quote"].AsString())

        let wsname = record.["wsname"].AsString().Split('/')
        let _base = mapBTC wsname.[0]
        let quote = mapBTC wsname.[1]

        pairs.Add(CurrencyPair(_base, quote))    

    pairs

let parseTicker (pair:CurrencyPair, data:string) =
    let result = load_result_and_check_errors data            

    let (name, values) = result.Properties().[0]
    
    let ask = values.Item("a").[0].AsDecimal()
    let bid = values.Item("b").[0].AsDecimal()

    Ticker(pair, bid, ask, None, None, None)


//    (*
//    <pair_name> = pair name
//    a = ask array(<price>, <whole lot volume>, <lot volume>),
//    b = bid array(<price>, <whole lot volume>, <lot volume>),
//    c = last trade closed array(<price>, <lot volume>),
//    v = volume array(<today>, <last 24 hours>),
//    p = volume weighted average price array(<today>, <last 24 hours>),
//    t = number of trades array(<today>, <last 24 hours>),
//    l = low array(<today>, <last 24 hours>),
//    h = high array(<today>, <last 24 hours>),
//    o = today's opening price
//    *)

//{
//"error": [],
//"result": {
//  "XXRPZEUR": {
//    "a": [ "0.26076000", "17300", "17300.000" ],
//    "b": [ "0.26075000", "77", "77.000" ],
//    "c": [ "0.26075000", "1386.03215000" ],
//    "v": [ "985104.92724731", "2259841.10057655" ],
//    "p": [ "0.26212413", "0.26350512" ],
//    "t": [ 751, 1749 ],
//    "l": [ "0.25921000", "0.25921000" ],
//    "h": [ "0.26559000", "0.26700000" ],
//    "o": "0.26430000"
//  }
//}


let parseBalance(jsonString:string) =
    let result = load_result_and_check_errors(jsonString)

    let currenciesBalance =
        result.Properties() 
        |> Seq.map (fun (kraken_currency, amountJson) -> 
                        let currency = currency_mapping.get_currency kraken_currency
                        let ownedAmount = amountJson.AsDecimal()
                        CurrencyBalance(Currency(currency), ownedAmount, ownedAmount)
                    )

    new AccountBalance(currenciesBalance)

    

let parseOrder(jsonString:string) =    
    let result = load_result_and_check_errors(jsonString)

    let order = result.["descr"].["order"].ToString()
    let amount = Decimal.Parse(order.Split(' ').[1])
    let orderIds = result.["txid"].AsArray() |> Array.map (fun v -> v.AsString())

    struct (orderIds, amount)


let parseOpenOrders(jsonString:string) = 
    let result = load_result_and_check_errors(jsonString)

    let orders = List<Order>()
    let ordersJson = result.["open"].Properties()
    for (orderId, order) in ordersJson do
        //let status = order.["status"]
        let timestamp = order.["opentm"].AsDecimal() // 1575484650.7296,
        let creationDate = DateTimeOffset.FromUnixTimeSeconds(int64 timestamp).DateTime
        //let creationDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime
        let data = order.["descr"]
        //let pair = CurrencyPair.parse(data.["pair"].AsString())
        let orderSide = match data.["type"].AsString() with
                        | "sell" -> OrderSide.Sell
                        | "buy" -> OrderSide.Buy
                        | _ -> failwithf "Order side not recognized: %s" (data.["type"].AsString())
            
        let orderType = match data.["ordertype"].AsString() with
                        | "limit" -> OrderType.Limit
                        | "market" -> OrderType.Market
                        | _ -> failwithf "Order type not recognized: %s" (data.["ordertype"].AsString())
        let orderAmount = Decimal.Parse(order.["vol"].AsString())
        let priceString = data.["price"].AsString()
        let price:Nullable<decimal> = Nullable<decimal>(Decimal.Parse(priceString)) // if limit orders      
     
        orders.Add (Order(orderId, creationDate, orderType, orderSide, Currency("xrp"), Currency("eur"), orderAmount, price) )
   
    orders.ToArray()
     

//refid = Referral order transaction id that created this order
//userref = user reference id
//status = status of order:
//    pending = order pending book entry
//    open = open order
//    closed = closed order
//    canceled = order canceled
//    expired = order expired
//opentm = unix timestamp of when order was placed
//starttm = unix timestamp of order start time (or 0 if not set)
//expiretm = unix timestamp of order end time (or 0 if not set)
//descr = order description info
//    pair = asset pair
//    type = type of order (buy/sell)
//    ordertype = order type (See Add standard order)
//    price = primary price
//    price2 = secondary price
//    leverage = amount of leverage
//    order = order description
//    close = conditional close order description (if conditional close set)
//vol = volume of order (base currency unless viqc set in oflags)
//vol_exec = volume executed (base currency unless viqc set in oflags)
//cost = total cost (quote currency unless unless viqc set in oflags)
//fee = total fee (quote currency)
//price = average price (quote currency unless viqc set in oflags)
//stopprice = stop price (quote currency, for trailing stops)
//limitprice = triggered limit price (quote currency, when limit based order type triggered)
//misc = comma delimited list of miscellaneous info
//    stopped = triggered by stop price
//    touched = triggered by touch price
//    liquidated = liquidation
//    partial = partial fill
//oflags = comma delimited list of order flags
//    viqc = volume in quote currency
//    fcib = prefer fee in base currency (default if selling)
//    fciq = prefer fee in quote currency (default if buying)
//    nompp = no market price protection

let startDate = DateTime(1970, 1, 1)

let parseDate dateNumber = startDate + TimeSpan.FromSeconds(float(dateNumber))

let parseClosedOrders (jsonString:string) =      
    let result = load_result_and_check_errors(jsonString)
       
    let readOrder (name, json:JsonValue) = 
        let descr:JsonValue = json.["descr"]
        let id = name
        let ``type`` = descr.["ordertype"].AsString()
        let side = descr.["type"].AsString()

        let openTime = parseDate(json.["opentm"].AsDecimal())
        let closeTime = parseDate(json.["closetm"].AsDecimal())
        let status = json.["status"].AsString()
        let reason = json.["reason"].AsString()

        let amount = 0m
        let price = json.["price"].AsDecimal()
        

        let vol = json.["vol"].AsDecimal()
        let vol_exec = json.["vol_exec"].AsDecimal()
        let buyQuantity = Math.Min(vol, vol_exec)

        let payQuantity = json.["cost"].AsDecimal()
        let fee = json.["fee"].AsDecimal()
        
        

        ClosedOrder(id, ``type``, side, openTime, closeTime, status, reason, buyQuantity, payQuantity, price, fee)


    result.["closed"].Properties() |> Array.map readOrder

    //let orders = List<ClosedOrder>()

    //orders

    (*
    "AAA5CK-GKYF6-HEMAAA": {
           "refid": null,
           "userref": 0,
           "status": "closed",
           "reason": null,
           "opentm": 1585496914.1998,
           "closetm": 1585496914.2097,
           "starttm": 0,
           "expiretm": 0,
           "descr": {
             "pair": "XRPEUR",
             "type": "buy",
             "ordertype": "market",
             "price": "0",
             "price2": "0",
             "leverage": "none",
             "order": "buy 2321.93000000 XRPEUR @ market",
             "close": ""
           },
           "vol": "2321.93000000",
           "vol_exec": "2321.93000000",
           "cost": "383.52689",
           "fee": "1.02316",
           "price": "0.15604",
           "stopprice": "0.00000000",
           "limitprice": "0.00000000",
           "misc": "",
           "oflags": "fciq"
         },
    *)

    
let parseWithdrawal(jsonString:string) =    
    let result = load_result_and_check_errors(jsonString)
    result.["refid"].AsString()