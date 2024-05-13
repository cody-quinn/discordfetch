module DiscordFetch.Process

open System

type ProcessResult =
  { ExitCode : int
    Stdout : string
    Stderr : string }

let startProcess name (args : string list) =
  let psi = Diagnostics.ProcessStartInfo (name, args)
  psi.UseShellExecute <- false
  psi.RedirectStandardOutput <- true
  psi.RedirectStandardError <- true
  psi.CreateNoWindow <- true

  let process' = Diagnostics.Process.Start psi
  let stdout = Text.StringBuilder ()
  let stderr = Text.StringBuilder ()

  process'.OutputDataReceived.Add (_.Data >> stdout.Append >> ignore)
  process'.ErrorDataReceived.Add (_.Data >> stderr.Append >> ignore)
  process'.BeginOutputReadLine ()
  process'.BeginErrorReadLine ()
  process'.WaitForExit ()

  { ExitCode = process'.ExitCode
    Stdout = string stdout
    Stderr = string stderr }
