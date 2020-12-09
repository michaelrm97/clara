namespace Shared

open System

type NextConfigRequest =
    { id : string }
    
type CurrentConfig =
    { id : Guid
      set : DateTime }

type CurrentConfigResponse =
    { id : string
      set : string }


type ErrorResponse =
    { statusCode : int
      message : string }
