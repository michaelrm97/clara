module Server

open Giraffe
open Saturn

open LightingConfiguration
open Storage

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

let storage = Storage()

storage.insertConfig Test.lightingConfig |> ignore

let listConfigs (ctx: HttpContext) : Task<HttpContext Option> = Controller.json ctx storage.listConfigs

let getConfig (ctx: HttpContext) (id: string) : Task<HttpContext Option> =
    let accept = ctx.TryGetRequestHeader "Accept"

    let mutable guid : Guid = Guid.NewGuid()
    if Guid.TryParse (id, &guid) |> not then
        ctx.SetStatusCode 400
        Controller.json ctx
            { statusCode = 400
              message = "id is invalid" }
    else

    match accept with
    | Some "application/octet-stream" ->
        match storage.getConfigBinary guid with
        | Ok result ->
            Controller.sendDownloadBinary ctx (List.toArray result)
        | Error e ->
            let response : byte[] = List.toArray [];
            ctx.SetStatusCode e.statusCode
            Controller.sendDownloadBinary ctx response
    | _ ->
        match storage.getConfig guid with
        | Ok result ->
            Controller.json ctx result
        | Error e ->
            ctx.SetStatusCode e.statusCode
            match accept with
            | Some "text/plain" -> Controller.text ctx e.message
            | _ -> Controller.json ctx e

let addConfig (ctx: HttpContext) : Task<HttpContext Option> = task {
    let! configJsonObject = Controller.getJson<LightingConfigurationInputObject> ctx

    if configJsonObject :> obj = null then
        ctx.SetStatusCode 400
        return! Controller.json ctx
            { statusCode = 400
              message = "Lighting configuration is empty" }
    else

    match storage.addConfig configJsonObject with
    | Ok result ->
        ctx.SetStatusCode 201
        return! Controller.json ctx result
    | Error e ->
        ctx.SetStatusCode e.statusCode
        return! Controller.json ctx e
}

let updateConfig (ctx: HttpContext) (id: string) : Task<HttpContext Option> = task {
    let! configJsonObject = Controller.getJson<LightingConfigurationInputObject> ctx

    let mutable guid : Guid = Guid.NewGuid()
    if Guid.TryParse (id, &guid) |> not then
        ctx.SetStatusCode 400
        return! Controller.json ctx
            { statusCode = 400
              message = "id is invalid" }
    else

    if configJsonObject :> obj = null then
        ctx.SetStatusCode 400
        return! Controller.json ctx
            { statusCode = 400
              message = "Lighting configuration is empty" }
    else

    match storage.updateConfig guid configJsonObject with
    | Ok () ->
        ctx.SetStatusCode 204
        return! Task.FromResult (Some ctx)
    | Error e ->
        ctx.SetStatusCode e.statusCode
        return! Controller.json ctx e
}

let deleteConfig (ctx: HttpContext) (id: string) : Task<HttpContext Option> =
    let mutable guid : Guid = Guid.NewGuid()
    if Guid.TryParse (id, &guid) |> not then
        ctx.SetStatusCode 400
        Controller.json ctx
            { statusCode = 400
              message = "id is invalid" }
    else

    match storage.deleteConfig guid with
    | Ok () ->
        ctx.SetStatusCode 204
        Task.FromResult (Some ctx)
    | Error e ->
        ctx.SetStatusCode e.statusCode
        Controller.json ctx e

type CurrentConfigResponse =
    { id : string
      set : string }

let returnCurrentConfig (next: HttpFunc) (ctx: HttpContext) (result: CurrentConfig Option) =
    match ctx.TryGetRequestHeader "Accept" with
    | Some "text/plain" ->
        let response =
            match result with
                | Some r -> sprintf "%s,%i" (r.id.ToString()) r.set.Ticks
                | None -> "None"
        Successful.OK response next ctx
    | _ ->
        let response =
            match result with
                | Some r ->
                    { id = r.id.ToString()
                      set = r.set.ToString() }
                | None ->
                    { id = null
                      set = null }
        Successful.OK response next ctx

let getCurrent : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let result = storage.getCurrent

        returnCurrentConfig next ctx result

let setNext : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! nextConfig = ctx.BindJsonAsync<NextConfigRequest>()

            let id = if nextConfig :> obj = null then null else nextConfig.id

            let nextConfig =
                let mutable guid : Guid = Guid.NewGuid()
                if Guid.TryParse (id, &guid) then
                    Some guid
                else None

            match storage.setCurrent nextConfig with
            | Ok result -> return! returnCurrentConfig next ctx result
            | Error e ->
                match ctx.TryGetRequestHeader "Accept" with
                | Some "text/plain" -> return! RequestErrors.BAD_REQUEST e.message next ctx
                | _ -> return! RequestErrors.BAD_REQUEST e next ctx
        }

let stop : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        storage.stop
        Successful.NO_CONTENT next ctx

let lightingController = controller {
    index listConfigs
    show getConfig
    create addConfig
    update updateConfig
    delete deleteConfig
}

let apiRouter = router {
    forward "/lighting" lightingController

    get "/current" getCurrent
    post "/next" setNext
    post "/stop" stop
}

let webApp = router {
    forward "/api" apiRouter
}

let app =
    application {
        url "http://0.0.0.0:8085"
        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

run app
