module Core.SParser
open System
open Core.Common
/// slutgiltliga valuen som returnas
type SValue =
  | SNull
  | SBool of bool
  | SString of string
  | SNumber of float
  | SArray of SValue list
  | SObject of Map<string, SValue>
  | SCommand of string * SValue option
/// infix som kör parser, ignorerar resultatet och returnar value
let (>>%) p x = p |>> fun _ -> x
/// retur av value null
let pValueNull =
  parseString "null"
  >>% SNull <?> "null"
/// return av value bool
let pValueBool =
  let sTrue =
    parseString "true"
    >>% SBool true
  let sFalse =
    parseString "false"
    >>% SBool false
  sTrue <|> sFalse <?> "bool"
/// return string utan value
let pString =
  let chars = anyOf (['a'..'z'] @ ['A'..'Z'] @ ['(';')'])
  let string =
    manyChars chars
  string
  <?> "string parser"
/// parsear escape-sekvenser i strängar
let pEscapedChar =
  parseChar '\\'
  >>. (
    (parseChar '\"' >>% '\"')
    <|> (parseChar '\\' >>% '\\')
    <|> (parseChar 'n' >>% '\n')
    <|> (parseChar 't' >>% '\t')
    <|> (parseChar 'r' >>% '\r')
  ) <?> "escaped char"
/// return av value "string" eller 'string'
let pQuotedString =
  let quote = parseChar '\"' <?> "quote"
  // acceptera alla tecken utom citattecken och backslash, eller escape-sekvenser
  let normalChar = satisfy (fun ch -> ch <> '\"' && ch <> '\\') "string char"
  let stringChar = pEscapedChar <|> normalChar
  quote >>. manyChars stringChar .>> quote
/// return av value string
let pValueString =
  pQuotedString
  |>> SString
  <?> "quoted string"
/// return av value int
let pValueNumber =
  let optMinus = opt (parseChar '-')
  let zero = parseString "0"
  let digitOneToNine = satisfy (fun ch -> Char.IsDigit ch && ch <> '0') "digits 1-9"
  let digit = satisfy (fun ch -> Char.IsDigit ch) "digit"
  let dot = parseChar '.'
  // let optPlusMinus = opt(parseChar '-' <|> parseChar '+')
  let nonZeroNumber =
    digitOneToNine .>>.manyChars digit
    |>> fun (first, rest) -> string first + rest
    
  let numberPart = zero <|> nonZeroNumber
  let fractionPart = dot >>. manyChars1 digit
  
  let convertToSNumber ((optSign, numberPart), fractionPart) =
    let (|>?) opt f =
      match opt with
      | None -> ""
      | Some x -> f x
    let signStr =
      optSign
      |>? string // "-"
    let fractionPartStr =
      fractionPart
      |>?  (fun digits -> "." + digits) // ".123"
    (signStr + numberPart + fractionPartStr)
    |> float
    |> SNumber
  
  optMinus .>>. numberPart .>>. opt fractionPart
  |>> convertToSNumber
  <?> "number"
let createParserForwardedToRef<'a>() =
  let dummyParser =
    let innerFn _ : Result<'a * string> = failwith "forward parser not set"
    {parseFn=innerFn;label="forward parser"}
  let parserRef = ref dummyParser
  let innerFn input =
    run parserRef.Value input
  let wrapperParser = {parseFn=innerFn;label="wrapper parser"}
  wrapperParser, parserRef
/// return av value "Skogix"
let sSharpValue, sValueRef = createParserForwardedToRef<SValue>() 
/// return av value array
let pValueArray =
  let left = parseChar '[' .>> spaces
  let right = parseChar ']' .>> spaces
  let comma = parseChar ',' .>> spaces
  let value = sSharpValue .>> spaces
  let values = separateBy value comma
  between left values right
  |>> SArray
  <?> "array"
// sätter potentiella forwarden till number
sValueRef.Value <- pValueNumber
let pValueObject =
  let left = parseChar '{' .>> spaces
  let right = parseChar '}' .>> spaces
  let colon = parseChar ':' .>> spaces
  let comma = parseChar ',' .>> spaces
  let key = pQuotedString .>> spaces
  let value = sSharpValue .>> spaces

  let keyValue = (key .>> colon) .>>. value
  let keyValues = separateBy keyValue comma

  between left keyValues right
  |>> Map.ofList
  |>> SObject
let pValueCommand =
  let command = pString .>> spaces
  let argument = opt (
                       pValueNull
                       <|> pValueBool
                       <|> pValueNumber
                       <|> pValueArray
                       <|> pValueString
                       <|> pValueObject
                       
                       .>> spaces
                     )
  let sCommand = command .>>. argument
// let argument = opt (sValue .>> spaces)
// let sCommand = command .>>. argument
  sCommand
  |>>  SCommand
sValueRef.Value <- choice
  [
// def av en slutgiltlig value
// detta ger prioritet / ordning
    pValueNull
    pValueBool
    pValueNumber
    pValueObject
    pValueString
    pValueArray
    pValueCommand
  ]
/// Convert SValue to JSON string with type/value format
let rec toJson (value: SValue) : string =
  let escape (s: string) =
    s.Replace("\\", "\\\\")
     .Replace("\"", "\\\"")
     .Replace("\n", "\\n")
     .Replace("\r", "\\r")
     .Replace("\t", "\\t")

  match value with
  | SNull ->
      """{"type": "null", "value": null}"""
  | SBool b ->
      sprintf """{"type": "bool", "value": %s}""" (if b then "true" else "false")
  | SNumber n ->
      sprintf """{"type": "number", "value": %g}""" n
  | SString s ->
      sprintf """{"type": "string", "value": "%s"}""" (escape s)
  | SArray items ->
      let jsonItems = items |> List.map toJson |> String.concat ", "
      sprintf """{"type": "array", "value": [%s]}""" jsonItems
  | SObject map ->
      let jsonPairs =
        map
        |> Map.toList
        |> List.map (fun (k, v) -> sprintf """"%s": %s""" (escape k) (toJson v))
        |> String.concat ", "
      sprintf """{"type": "object", "value": {%s}}""" jsonPairs
  | SCommand (name, argOpt) ->
      match argOpt with
      | None -> sprintf """{"type": "command", "name": "%s"}""" (escape name)
      | Some arg -> sprintf """{"type": "command", "name": "%s", "arg": %s}""" (escape name) (toJson arg)

let printSParse (input:string) =
  match run sSharpValue input with
  | ParseSuccess (value, _) -> printfn "%s" (toJson value)
  | ParseFailure (label, error) -> printfn "Error parsing %s\n%s" label error

let getSParse (input:string) = run sSharpValue input
