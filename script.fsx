#load ".paket/load/main.group.fsx" 

open System
open System.Collections.Concurrent
open CosmoStore
open CosmoStore.InMemory
open Thoth.Json.Net
open Microsoft.FSharp.Reflection

let config : Configuration =
    { InMemoryStreams = ConcurrentDictionary<string,Stream>()
      InMemoryEvents = ConcurrentDictionary<Guid, EventRead>() }
    
let store = EventStore.getEventStore config

type Event =
    | Increase of int
    | Decrease of int

let encodeEvent = Encode.Auto.generateEncoder<Event>()
let decodeEvent = Decode.Auto.generateDecoder<Event>()

let getUnionCaseName (x:'a) = 
    match FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name  

let createEvent event =
    { Id = (Guid.NewGuid())
      CorrelationId = None
      CausationId = None
      Name = getUnionCaseName event
      Data = encodeEvent event
      Metadata = None }
    
let appendEvent event =
    store.AppendEvent "CounterStream" ExpectedPosition.Any event
    |> Async.AwaitTask
    |> Async.RunSynchronously

let increase amount =
    Increase(amount)
    |> createEvent
    |> appendEvent
    
let decrease amount =
    Decrease(amount)
    |> createEvent
    |> appendEvent

increase 5
decrease 3
increase 198
decrease 203
increase 4

store.GetEvents "CounterStream" EventsReadRange.AllEvents
|> Async.AwaitTask
|> Async.RunSynchronously
|> List.fold (fun acc e ->
    match Decode.fromValue "$" decodeEvent e.Data with
    | Ok(Increase i) -> acc + i
    | Ok(Decrease d) -> acc - d
    | Error e ->
        failwithf "invalid event from store, %s" e
) 0
|> printfn "Projected state is %d"