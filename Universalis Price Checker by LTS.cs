import requests
import time
from functools import lru_cache
from typing import Dict, Tuple
from xivapi import Client
from pydantic import BaseModel
from urllib.parse import quote

import clr
clr.AddReference('Dalamud.Game')
clr.AddReference('Dalamud.Game.ClientState')
clr.AddReference('Dalamud.Plugin')

from Dalamud.Game.ClientState import ClientState
from Dalamud.Game.ClientState.Objects.SubKinds import InventoryItem
from Dalamud.Game.Gui import ImGui, ItemContextMenu
from Dalamud.Plugin import Plugin, PluginCommand, PluginCommandManager

CACHE_SIZE = 100
API_CALL_INTERVAL = 5  # In seconds

class ServerPrices(BaseModel):
    min_price: int = 0
    max_price: int = 0


class UniversalisPriceChecker(Plugin):
    Name = "Universalis Price Checker"
    pluginInterface: 'DalamudPluginInterface'
    commandManager: PluginCommandManager
    xivapi: Client
    searchInput: str = ""
    selectedServers: Dict[str, ServerPrices] = {
        "Aether": ServerPrices(),
        "Crystal": ServerPrices(),
        "Dynamis": ServerPrices(),
        "Primal": ServerPrices(),
    }
    last_api_call: float = 0

    def __init__(self, pluginInterface: 'DalamudPluginInterface'):
        super().__init__(pluginInterface)
        self.pluginInterface = pluginInterface
        self.commandManager = PluginCommandManager(self)

        self.pluginInterface.ItemContextMenu.AddItem("Check Universalis Prices", self.handle_context_menu_action, self.can_run_context_menu_action)

        self.xivapi = Client()

    def dispose(self):
        self.pluginInterface.ItemContextMenu.RemoveItem("Check Universalis Prices", self.handle_context_menu_action)

    def can_run_context_menu_action(self, context: ItemContextMenu) -> bool:
        return context and context.Item

    def handle_context_menu_action(self, context: ItemContextMenu) -> None:
        item_name = context.Item.Name
        self.pluginInterface.Framework.Gui.Chat.Print(f"Checking prices for {item_name}...")
        self.commandManager.Execute(f"/upc {item_name}")

    def draw_ui(self):
        ImGui.SetNextWindowSize((300, 100), ImGuiCond.FirstUseEver)
        ImGui.Begin("Universalis Price Checker")

        ImGui.InputText("Search for an item", self.searchInput, 255)
        if ImGui.Button("Check Prices") and len(self.searchInput) > 0:
            self.commandManager.Execute(f"/upc {self.searchInput}")

        ImGui.End()

    @PluginCommand("/upc")
    def check_price(self, args: List[str]) -> None:
        item_name = " ".join(args)
        if not item_name:
            return

        item = self.get_item_by_name(item_name)

        if item is None:
            self.pluginInterface.Framework.Gui.Chat.PrintError(f"Could not find item \"{item_name}\".")
            return

        item_info = self.get_item_info(item.itemId)

        if not item_info:
            self.pluginInterface.Framework.Gui.Chat.PrintError(f"Could not find price information for \"{item.Name}\".")
            return

        self.print_prices(item_info, item.Name)

    def get_item_by_name(self, name: str) -> InventoryItem:
        inventory = self.pluginInterface.ClientState.LocalPlayer.inventory
        return next((item for item in inventory if item.Name.lower() == name.lower()), None)
        
    @lru_cache(maxsize=CACHE_SIZE)
    def get_item_info(self, item_id: int) -> Dict[str, Dict[str, int]]:
            current_time = time.time()
        time_since_last_call = current_time - self.last_api_call

        if time_since_last_call < API_CALL_INTERVAL:
            self.pluginInterface.Framework.Gui.Chat.PrintError(f"Please wait {API_CALL_INTERVAL - time_since_last_call:.1f} seconds before checking prices again.")
            return None

        self.last_api_call = current_time
        
        try:
            item_name = self.xivapi.indexes("Item").ids(item_id)[0].name
            encoded_name = quote(item_name)
            item_info = {}
            for server, prices in self.selectedServers.items():
                if prices.min_price == 0 and prices.max_price == 0:
                    continue

                endpoint = f"https://universalis.app/api/{server}/market/{encoded_name}"
                response = requests.get(endpoint)

                if not response.ok:
                    continue

                data = response.json()
                item_info[server] = {"min_price": data["listings"][0]["pricePerUnit"], "max_price": data["listings"][-1]["pricePerUnit"]}

            return item_info
        except Exception as e:
            self.pluginInterface.Framework.Gui.Chat.PrintError(f"Could not fetch price information: {str(e)}")
            return None

    def print_prices(self, item_info: Dict[str, Dict[str, int]], item_name: str):
        self.pluginInterface.Framework.Gui.Chat.Print(f"Prices for {item_name}:")
        for server, prices in item_info.items():
            self.pluginInterface.Framework.Gui.Chat.Print(f"\t{server}: {prices['min_price']:,} - {prices['max_price']:,}")

    def on_config_reload(self, plugin: Plugin):
        self.load_configuration()

    def load_configuration(self):
        config = self.pluginInterface.LoadPluginConfig()
        self.selectedServers["Aether"].min_price = config.get("AetherMinPrice", 0)
        self.selectedServers["Aether"].max_price = config.get("AetherMaxPrice", 0)
        self.selectedServers["Crystal"].min_price = config.get("CrystalMinPrice", 0)
        self.selectedServers["Crystal"].max_price = config.get("CrystalMaxPrice", 0)
        self.selectedServers["Dynamis"].min_price = config.get("DynamisMinPrice", 0)
        self.selectedServers["Dynamis"].max_price = config.get("DynamisMaxPrice", 0)
        self.selectedServers["Primal"].min_price = config.get("PrimalMinPrice", 0)
        self.selectedServers["Primal"].max_price = config.get("PrimalMaxPrice", 0)

    def save_configuration(self):
        config = {
            "AetherMinPrice": self.selectedServers["Aether"].min_price,
            "AetherMaxPrice": self.selectedServers["Aether"].max_price,
            "CrystalMinPrice": self.selectedServers["Crystal"].min_price,
            "CrystalMaxPrice": self.selectedServers["Crystal"].max_price,
            "DynamisMinPrice": self.selectedServers["Dynamis"].min_price,
            "DynamisMaxPrice": self.selectedServers["Dynamis"].max_price,
            "PrimalMinPrice": self.selectedServers["Primal"].min_price,
            "PrimalMaxPrice": self.selectedServers["Primal"].max_price,
    }
    self.pluginInterface.SavePluginConfig(config)

def on_draw(self):
    self.draw_ui()

def on_open_config(self):
    self.pluginInterface.Framework.Gui.SaveWindowSettings()
    self.pluginInterface.Framework.GuiBuilder.Menu()
    if ImGui.BeginPopupModal("Universalis Price Checker Settings", None, ImGuiWindowFlags.AlwaysAutoResize):
        ImGui.Text("Select minimum and maximum prices for each server.")
        ImGui.Spacing()

        for server, prices in self.selectedServers.items():
            ImGui.Text(server)

            ImGui.SameLine(ImGui.GetWindowWidth() * 0.5)

            ImGui.InputInt(f"Min##{server}", prices.min_price, 10000, 1000000)
            ImGui.SameLine()

            ImGui.InputInt(f"Max##{server}", prices.max_price, 10000, 1000000)

        if ImGui.Button("Save", (50, 0)):
            self.save_configuration()
            ImGui.CloseCurrentPopup()

        ImGui.EndPopup()

def on_initialize(self):
    self.load_configuration()
    self.pluginInterface.Framework.GuiBuilder.OnOpenConfig += self.on_open_config
    self.pluginInterface.Framework.GuiBuilder.OnDraw += self.on_draw
    self.pluginInterface.Framework.OnUpdateEvent += self.on_update_event

def on_update_event(self, _):
    pass

def on_shutdown(self):
    self.dispose()
