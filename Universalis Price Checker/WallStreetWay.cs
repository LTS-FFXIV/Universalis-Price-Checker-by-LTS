using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Addons;
using Dalamud.Plugin;
using ImGuiNET;

public class ServerPrices
{
    public int MinPrice { get; set; } = 0;
    public int MaxPrice { get; set; } = 0;
}

public class UniversalisPriceChecker : IDalamudPlugin
{
    private const int CacheSize = 100;
    private const int ApiCallInterval = 5; // In seconds
    private const int CacheDuration = 300; // In seconds (5 minutes)

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly PluginCommandManager<UniversalisPriceChecker> _commandManager;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, ServerPrices> _selectedServers;
    private double _lastApiCall;
    private string _searchInput = "";

    private readonly Dictionary<uint, (DateTime fetchedAt, Dictionary<string, Dictionary<string, int>> itemInfo)> _itemInfoCache
        = new Dictionary<uint, (DateTime, Dictionary<string, Dictionary<string, int>>>()>;

    public string Name => "Universalis Price Checker";

    public UniversalisPriceChecker(DalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        _commandManager = new PluginCommandManager<UniversalisPriceChecker>(this);
        _httpClient = new HttpClient();

        _selectedServers = new Dictionary<string, ServerPrices>
        {
            { "Aether", new ServerPrices() },
            { "Crystal", new ServerPrices() },
            { "Dynamis", new ServerPrices() },
            { "Primal", new ServerPrices() },
        };

        _pluginInterface.UiBuilder.OnBuildUi += DrawUI;
        _pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => ImGui.OpenPopup("Universalis Price Checker Settings");
        _commandManager.AddHandler("/upc", new PluginCommand(command => CheckPrice(command), "Check item prices on Universalis.", "/upc <item name>"));
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/upc");
        _pluginInterface.UiBuilder.OnBuildUi -= DrawUI;
    }

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
            }

            if (ImGui.Button("Save and Close"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

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
            var jsonString = await response.Content.ReadAsStringAsync();
            var universalisResponse = JsonSerializer.Deserialize<UniversalisResponse>(jsonString);

            var itemInfo = new Dictionary<string, Dictionary<string, int>>();

            foreach (var listing in universalisResponse.listings)
            {
                if (!itemInfo.ContainsKey(listing.server))
                {
                    itemInfo[listing.server] = new Dictionary<string, int>();
                }

                itemInfo[listing.server][listing.retainer] = listing.pricePerUnit;
            }

            _itemInfoCache[itemId] = (DateTime.UtcNow, itemInfo);
            _lastApiCall = DateTime.UtcNow;

            return itemInfo;
        }

        return null;
    }
}

public class UniversalisResponse
{
    public List<UniversalisListing> listings { get; set; }
}

public class UniversalisListing
{
    public string retainer { get; set; }
    public string server { get; set; }
    public int pricePerUnit { get; set; }
}

