﻿module UnitTests.parser

open System
open System.IO
open NUnit.Framework
open FsUnit
open Alex75.Cryptocurrencies



let loadApiResponse fileName =
    File.ReadAllText(Path.Combine( "data", fileName))

[<Test>]
let parse_ticker () =

    let pair = CurrencyPair.XRP_USD
    let json = loadApiResponse "GET ticker response.json"
    let ticker = parser.parseTicker (pair, json)

    ticker.Currencies |> should equal pair
    ticker.Ask |> should equal 0.26076000
    ticker.Bid |> should equal 0.26075000


[<Test>]
let ``parse_balance when is error``() =

    let json = loadApiResponse "Balance response - error.json"

    (fun () -> parser.parse_balance(json) |> ignore) |> should throw typeof<Exception>



[<Test>]
let ``parse_balance`` () =

    let json = loadApiResponse "Balance response.json"
    
    let balance = parser.parse_balance(json)
    
    balance |> should not' (be null)
    balance.Keys |> should contain ("USD")
    balance.Keys |> should contain ("EUR")
    balance.Keys |> should contain ("XRP")
    balance.Keys |> should contain ("LTC")

    balance.["USD"] |> should equal 0m
    balance.["EUR"] |> should equal 501
    balance.["XRP"] |> should equal 0.68765056
    balance.["LTC"] |> should equal 0.0000042500


[<Test>]
let ``parse_order`` () =

    let json = loadApiResponse "create market order response.json"
    
    let struct (orderIds, amount) = parser.parse_order(json)
    
    orderIds |> should contain "O5PWAY-435NAD-6NAI7P"
    amount |> should equal 100.00000000