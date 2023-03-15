using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;

/// <summary>
/// Represents the minimum and maximum prices for a server.
/// </summary>
public class ServerPrices
{
    public int MinPrice { get; set; } = 0;
    public int MaxPrice { get; set; } = 0;
}

/// <summary>
/// Universalis Price Checker plugin for Dalamud.
/// </summary>
public class UniversalisPriceChecker : IDalamudPlugin
{
    // Constants
    private const int CacheSize = 100;
    private const int ApiCallInterval = 5; // In seconds
    private const int CacheDuration = 300; // In seconds (5 minutes)

    // Fields
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly PluginCommandManager<UniversalisPriceChecker> _commandManager;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, ServerPrices> _selectedServers;
    private DateTime _lastApiCall;
    private string _searchInput = "";
    private readonly Dictionary<uint, (DateTime fetchedAt, Dictionary<string, Dictionary<string, int>> itemInfo)> _itemInfoCache
        = new Dictionary<uint, (DateTime, Dictionary<string, Dictionary<string, int>>>()>();

    /// <summary>
    /// Gets the name of the plugin.
    /// </summary>
    public string Name => "Universalis Price Checker";

    /// <summary>
    /// Initializes a new instance of the UniversalisPriceChecker class.
    /// </summary>
    /// <param name="pluginInterface">The Dalamud plugin interface.</param>
    public UniversalisPriceChecker(DalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        _commandManager = new PluginCommandManager<UniversalisPriceChecker>(this);
        _httpClient = new HttpClient();
        _lastApiCall = DateTime.MinValue;

        _selectedServers = new Dictionary<string, ServerPrices>
        {
            { "Aether", new ServerPrices { MinPrice = 0, MaxPrice = 0 } },
            { "Crystal", new ServerPrices { MinPrice = 0, MaxPrice = 0 } },
            { "Dynamis", new ServerPrices { MinPrice = 0, MaxPrice = 0 } },
            { "Primal", new ServerPrices { MinPrice = 0, MaxPrice = 0 } },
        };

        _pluginInterface.UiBuilder.OnBuildUi += DrawUI;
        _pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => ImGui.OpenPopup("Universalis Price Checker Settings");
        _commandManager.AddHandler("/upc", new PluginCommand(command => CheckPrice(command), "Check item prices on Universalis.", "/upc <item name>"));
    }

/// <summary>
/// Disposes the plugin.
/// </summary>
public void Dispose()
{
    _commandManager.RemoveHandler("/upc");
    _pluginInterface.UiBuilder.OnBuildUi -= DrawUI;
    _httpClient.Dispose(); // Dispose of the HttpClient.
}
/// <summary>
/// Draws the plugin's user interface.
/// </summary>
    private void DrawUI()
    {
    ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 100), ImGuiCond.FirstUseEver);
    ImGui.Begin("Universalis Price Checker");

        ImGui.InputText("Search for an item", ref _searchInput, 255);
        if (ImGui.Button("Check Prices") && !string.IsNullOrEmpty(_searchInput))
        {
            _commandManager.Execute($"/upc {_searchInput}");
        }

    ImGui.End();

    if (ImGui.BeginPopupModal("Universalis Price Checker Settings", ref var isOpen, ImGuiWindowFlags.AlwaysAutoResize))
    {
        ImGui.Text("Select minimum and maximum prices for each server.");
        ImGui.Spacing();

        foreach (var server in _selectedServers.Keys.ToList())
        {
            ImGui.Text(server);
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.5f);
            ImGui.InputInt($"Min##{server}", ref _selectedServers[server].MinPrice, 10000, 1000000);
            ImGui.SameLine();
            ImGui.InputInt($"Max##{server}", ref _selectedServers[server].MaxPrice, 10000, 1000000);
            
            if (_selectedServers[server].MinPrice > _selectedServers[server].MaxPrice)
            {
                PluginLog.Error($"{nameof(UniversalisPriceChecker)}: Minimum price cannot be greater than maximum price for server {server}. Please adjust the values.");
            }
        }

    if (ImGui.Button("Save and Close"))
    {
        ImGui.CloseCurrentPopup();
    }

    ImGui.EndPopup();
}

/// <summary>
/// Checks the price of the specified item.
/// </summary>
/// <param name="input">The item name.</param>
    private async void CheckPrice(string input)
    {
        var itemName = input.Trim();
        if (string.IsNullOrEmpty(itemName)) return;

        var item = _pluginInterface.ClientState.Client.StaticItemList.FirstOrDefault(x => x.Value.Name.ToString().ToLower() == itemName.ToLower()).Value;

        if (item != null)
        {
            var itemInfo = await GetItemInfo(item.RowId);
            if (itemInfo != null)
            {
                foreach (var server in _selectedServers)
                {
                    var serverInfo = itemInfo.GetValueOrDefault(server.Key);
                    if (serverInfo != null)
                    {
                        var minPrice = server.Value.MinPrice;
                        var maxPrice = server.Value.MaxPrice;

                        var itemsInRange = serverInfo.Values.Count(x => x >= minPrice && x <= maxPrice);

                        PluginLog.Information($"[{server.Key}] {item.Name}: {itemsInRange} listings within the price range of {minPrice} to {maxPrice}");
                    }
                }
            }
        }
        else
        {
            PluginLog.Information($"Could not find item: {itemName}");
        }
    }

/// <summary>
/// Retrieves item information from the Universalis API.
/// </summary>
/// <param name="itemId">The item ID.</param>
/// <returns>A dictionary containing server and retainer price information for the item.</returns>
private async Task<Dictionary<string, Dictionary<string, int>>> GetItemInfo(uint itemId)
{
    if (_itemInfoCache.TryGetValue(itemId, out var cachedItemInfo) && (DateTime.UtcNow - cachedItemInfo.fetchedAt).TotalSeconds < CacheDuration)
    {
        return cachedItemInfo.itemInfo;
    }

    if ((DateTime.UtcNow - _lastApiCall).TotalSeconds < ApiCallInterval) return null;

    var query = HttpUtility.ParseQueryString(string.Empty);
    query["ids"] = itemId.ToString();

    foreach (var server in _selectedServers.Keys)
    {
        query["servers"] = server;
    }

    var builder = new UriBuilder("https://universalis.app/api/extra");
    builder.Query = query.ToString();

    var response = await _httpClient.GetAsync(builder.Uri);    
    if (response.IsSuccessStatusCode)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        var universalisResponse = JsonSerializer.Deserialize<UniversalisResponse>(responseBody);

        var itemInfo = new Dictionary<string, Dictionary<string, int>>();

        foreach (var listing in universalisResponse.Listings)
        {
            if (!itemInfo.ContainsKey(listing.Server))
            {
                itemInfo[listing.Server] = new Dictionary<string, int>();
            }

            itemInfo[listing.Server][listing.Retainer] = listing.PricePerUnit;
        }

        _itemInfoCache[itemId] = (DateTime.UtcNow, itemInfo);
        _lastApiCall = DateTime.UtcNow;

        return itemInfo;
    }

    return null;
}

/// <summary>
/// Represents the response from the Universalis API.
/// </summary>
public class UniversalisResponse
{
    public List<UniversalisListing> Listings { get; set; }
}

/// <summary>
/// Represents a single listing in the Universalis API response.
/// </summary>
public class UniversalisListing
{
    public string Retainer { get; set; }
    public string Server { get; set; }
    public int PricePerUnit { get; set; }
}
