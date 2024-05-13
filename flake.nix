{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/master";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = {
    self,
    nixpkgs,
    flake-utils,
    ...
  }:
    flake-utils.lib.eachDefaultSystem (system: let
      pkgs = import nixpkgs {
        inherit system;
      };
    in
      {
        devShells.default = pkgs.mkShell {
          packages = with pkgs; [
            nuget-to-nix
            dotnet-sdk_8
          ];
          DOTNET_ROOT = "${pkgs.dotnet-sdk_8}";
        };
        packages.default = pkgs.buildDotnetModule rec {
          pname = "discordfetch";
          version = "1.0.0";
          src = ./src;

          projectFile = "DiscordFetch/DiscordFetch.fsproj";
          nugetDeps = ./deps.nix;

          dotnet-sdk = pkgs.dotnet-sdk_8;
          dotnet-runtime = pkgs.dotnet-runtime_8;

          buildType = "Release";
        };
      });
}
