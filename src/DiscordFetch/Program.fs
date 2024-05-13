module DiscordFetch.Program

open System
open System.IO
open Argu
open DiscordFetch.Process
open DiscordRPC
open DiscordRPC.Logging

[<RequireQualifiedAccess>]
module String =
  let split (sep : string) (str : string) = str.Split sep

[<RequireQualifiedAccess>]
module Map =
  let findOrDefault key default' table = Map.tryFind key table |> Option.defaultValue default'

let parseKvString =
  String.split "\n"
  >> Array.filter (String.IsNullOrEmpty >> not)
  >> Array.map (String.split "=")
  >> Array.map (fun inner -> inner[0], inner[1].Trim '"')
  >> Map

let tryReadFile path =
  try
    File.ReadAllText path |> Some
  with ex ->
    None

let tryReadKvFile = tryReadFile >> Option.map parseKvString

[<RequireQualifiedAccess>]
type OsId =
  | Arch
  | Debian
  | Fedora
  | Gentoo
  | NixOS
  | Void
  | Unknown

  override this.ToString () =
    match this with
    | Arch -> "arch"
    | Debian -> "debian"
    | Fedora -> "fedora"
    | Gentoo -> "gentoo"
    | NixOS -> "nixos"
    | Void -> "void"
    | Unknown -> "unknown"

[<RequireQualifiedAccess>]
module OsId =
  let fromIdString str =
    match str with
    | "arch" -> OsId.Arch
    | "debian" -> OsId.Debian
    | "fedora" -> OsId.Fedora
    | "gentoo" -> OsId.Gentoo
    | "nixos" -> OsId.NixOS
    | "void" -> OsId.Void
    | _ -> OsId.Unknown

type OsDetails =
  { Id : OsId
    Name : string
    Version : string }

  override this.ToString () = $"{this.Name} {this.Version}"

let getOs () =
  match tryReadKvFile "/etc/os-release" with
  | Some details ->
    let id = Map.findOrDefault "ID" "unknown" details
    let name = Map.findOrDefault "NAME" id details
    let version = Map.findOrDefault "VERSION" "" details

    { Id = OsId.fromIdString id
      Name = name
      Version = version }
  | None ->
    { Id = OsId.Unknown
      Name = "Unknown"
      Version = "" }

let getKernel () =
  let kernelName = (startProcess "uname" [ "-s" ]).Stdout.Trim ()
  let kernelRelease = (startProcess "uname" [ "-r" ]).Stdout.Trim ()
  $"{kernelName} {kernelRelease}"

let getUptime () =
  match tryReadFile "/proc/uptime" with
  | Some contents ->
    let uptime = (String.split "." contents)[0] |> float |> TimeSpan.FromSeconds

    let addEntry value suffix =
      if value > 0 then
        fun curr ->
          let suffix = if value > 1 then $"{suffix}s" else suffix
          $"{value} {suffix}" :: curr
      else
        id

    []
    |> addEntry uptime.Minutes "min"
    |> addEntry uptime.Hours "hour"
    |> addEntry uptime.Days "day"
    |> String.concat ", "
  | None -> ""

let mkActivity (os : OsDetails) kernel uptime buttons =
  RichPresence (
    Details = string os,
    State = $"{kernel} - {uptime}",
    Assets =
      Assets (
        LargeImageKey = string os.Id,
        LargeImageText = string os,
        SmallImageKey = "linux",
        SmallImageText = kernel
      ),
    Buttons = buttons
  )

type CliArguments =
  | ClientId of clientId : string
  | Button of name : string * url : string

  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | ClientId _ -> "Specify a client id"
      | Button _ -> "Add a button to the profile (max: 2)"

[<EntryPoint>]
let main args =
  let parser = ArgumentParser.Create<CliArguments> (checkStructure = false)
  let args = parser.Parse (args, raiseOnUsage = false)

  if args.IsUsageRequested then
    let usage = parser.PrintUsage ()
    printfn $"%s{usage}"
    exit 0

  // Constructing buttons provided by user
  let buttons =
    args.GetResults Button
    |> List.map (fun (label, url) -> DiscordRPC.Button (Label = label, Url = url))
    |> List.toArray

  // Opening up the client
  let clientId = args.GetResult (ClientId, defaultValue = "1239256891977105439")
  let discord = new DiscordRpcClient (clientId, Logger = ConsoleLogger ())
  let success = discord.Initialize ()

  if not success then
    eprintfn "Failed to connect to Discord RPC"
    exit 1

  // Main program loop
  while true do
    let os = getOs ()
    let kernel = getKernel ()
    let uptime = getUptime ()
    let activity = mkActivity os kernel uptime buttons
    discord.SetPresence activity
    Threading.Thread.Sleep 10_000

  0
