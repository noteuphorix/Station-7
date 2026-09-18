using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.Maps;

/// <summary>
/// Loads every map, re-rolls any tile variant that is out of range for its current tile
/// definition (e.g. left over from a tile prototype losing variants in a later update), and
/// resaves only the files that needed fixing.
/// </summary>
[AdminCommand(AdminFlags.Host)]
public sealed partial class FixTileVariantsCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private ILogManager _log = default!;

    public override string Command => "fixtilevariants";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var loader = _entManager.System<MapLoaderSystem>();
        var mapsSystem = _entManager.System<SharedMapSystem>();
        var tileSystem = _entManager.System<TileSystem>();
        var turfSystem = _entManager.System<TurfSystem>();

        var opts = MapLoadOptions.Default with
        {
            DeserializationOptions = DeserializationOptions.Default with
            {
                StoreYamlUids = true,
                LogOrphanedGrids = false
            }
        };

        var log = _log.GetSawmill(Command);
        var files = _res.ContentFindFiles(new ResPath("/Maps/")).ToList();
        var totalFixedFiles = 0;
        var totalFixedTiles = 0;

        for (var i = 0; i < files.Count; i++)
        {
            var fn = files[i];
            log.Info($"Checking file {i}/{files.Count} : {fn}");

            if (!loader.TryLoadGeneric(fn, out var result, opts))
                continue;

            if (result.Maps.Count > 1)
            {
                shell.WriteError($"Multi-map files like {fn} are not supported by {Command}, skipping.");
                loader.Delete(result);
                continue;
            }

            var fixedInFile = 0;

            foreach (var grid in result.Grids)
            {
                foreach (var tile in mapsSystem.GetAllTiles(grid.Owner, grid.Comp))
                {
                    var def = turfSystem.GetContentTileDefinition(tile);
                    if (tile.Tile.Variant < def.Variants)
                        continue;

                    var newTile = new Tile(tile.Tile.TypeId, tile.Tile.Flags, tileSystem.PickVariant(def),
                        tile.Tile.RotationMirroring);
                    mapsSystem.SetTile(grid.Owner, grid.Comp, tile.GridIndices, newTile);
                    fixedInFile++;
                }
            }

            if (fixedInFile == 0)
            {
                loader.Delete(result);
                continue;
            }

            _entManager.CullRemovedComponents();

            var map = result.Maps.FirstOrDefault();
            bool saveSuccess;

            if (map.Owner != default && _entManager.HasComponent<LoadedMapComponent>(map))
            {
                saveSuccess = loader.TrySaveMap(map.Comp.MapId, fn);
            }
            else if (result.Grids.Count == 1)
            {
                saveSuccess = loader.TrySaveGrid(result.Grids.First().Owner, fn);
            }
            else
            {
                shell.WriteError($"Failed to resave {fn} after fixing {fixedInFile} tile(s): ambiguous map/grid.");
                loader.Delete(result);
                continue;
            }

            if (saveSuccess)
            {
                log.Info($"Fixed {fixedInFile} tile(s) in {fn} and resaved it.");
                totalFixedFiles++;
                totalFixedTiles += fixedInFile;
            }
            else
            {
                shell.WriteError($"Fixed {fixedInFile} tile(s) in {fn} but failed to resave it.");
            }

            loader.Delete(result);
        }

        shell.WriteLine($"Done. Fixed {totalFixedTiles} tile(s) across {totalFixedFiles} file(s).");
    }
}
