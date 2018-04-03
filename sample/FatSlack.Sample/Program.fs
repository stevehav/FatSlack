﻿// Learn more about F# at http://fsharp.org

open System
open FatSlack.Bot
open FatSlack.Types
open FatSlack.Dsl
open FatSlack.Dsl.Types

let leetHandler : EventHandler =
    fun slackApi event ->
        async {
            let pongMessage = { event with Text = "Pong 1337" }
            printfn "==> Ponging: %A" pongMessage
            let! response = slackApi.PostMessage pongMessage
            do! Async.Sleep 3000
            let updateMessage = { response.Message with Text = "Updated Pong 1337"; Channel = response.Channel}
            printfn "==> Updating: %A" updateMessage
            do! slackApi.UpdateMessage updateMessage |> Async.Ignore
        } |> Async.RunSynchronously

let leetMatcher : EventMatcher =
    fun commandText event -> 
        let isMatch = commandText = "1337"
        printfn "==> Is it a match? %A" isMatch
        isMatch

let buttonHandler : EventHandler =
    fun slackApi event ->
        let actions = [
            Action.createAction (Button ("mybutton","Click me", "clicked"))
        ]
        let attachments = [
            (
                Attachment.createAttachment "callbackid"
                |> Attachment.withText "Some attachment"
                |> Attachment.withActions actions
            )
        ]
        let message =
            ChatMessage.createMessage event.Channel
            |> ChatMessage.withText "Show a button"
            |> ChatMessage.withAttachments attachments
        slackApi.PostMessage message |> Async.RunSynchronously |> ignore

let buttonMatcher : EventMatcher =
    fun commandText _ -> 
        let isMatch = commandText = "button"
        printfn "==> Is it a match? %A" isMatch
        isMatch

let createBot token =
    token
    |> init
    |> withAlias "jarvis"
    |> withSlackCommand {
        Syntax = "1337"
        Description = "Return a pong and then updates it"
        EventHandler = leetHandler
        EventMatcher = leetMatcher
    }
    |> withSlackCommand {
        Syntax = "button"
        Description = "Return a button"
        EventHandler = buttonHandler
        EventMatcher = buttonMatcher
    }
    |> start


open Suave
open Suave.Filters
open Suave.Operators
open System.Threading
open FatSlack.App
[<EntryPoint>]
let main argv =
    let cfg =
            { defaultConfig with
                  bindings =
                    [ HttpBinding.createSimple HTTP "0.0.0.0" 8080 ] }

    let logRequest : WebPart =
        fun (ctx: HttpContext) ->
            async {
                printfn "Request: %A" ctx.request
                return (Some ctx)
            }

    let healthCheck =
        choose
            [
                path "/health/ready" >=> Successful.OK "Ready"
                path "/health/health" >=> Successful.OK "Healthy"
            ]

    let handleRequest =
        request(fun req ->
            match req.formData "payload" with
            | Choice1Of2 payloadStr ->
                payloadStr 
                |> (createRequestHandler "" (FatSlack.SlackApi.createSlackApi ""))
                |> (sprintf "%A")
                |> Successful.OK
            | Choice2Of2 s ->
                printfn "Missing payload: %s" s
                Successful.OK ""
        )

    let app = 
        choose [
            healthCheck
            logRequest >=>
            choose
                [
                    path "/action" >=> handleRequest
                ]
        ]

    printfn "Starting web server"
    let cts = new CancellationTokenSource()
    let listening, server = startWebServerAsync cfg app
    Async.Start(server, cts.Token)

    let token = argv.[0]
    createBot token
    printfn "Hello World from F#! %s" argv.[0]
    System.Threading.Thread.Sleep(Int32.MaxValue)
    0 // return an integer exit code
