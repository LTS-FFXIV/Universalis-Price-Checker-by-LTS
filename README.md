# Universalis-Price-Checker-by-LTS
Does what it says on the tin


# More specifically

This is a FFXIV plugin that allows you to check the market prices of an item on different servers through the Universalis API. It can be used by right-clicking on an item in the inventory to get the context menu and select "Check Universalis Prices". The user can also input an item name in the search bar and click "Check Prices". The plugin fetches the item's ID, looks up its name on the Universalis API, and retrieves the minimum and maximum prices for the item on each of the selected servers. The prices are then printed in the chat window.

The plugin uses the following libraries:

requests: used to make HTTP requests to the Universalis API
xivapi: used to retrieve an item's ID from its name
pydantic: used to create a model for the server prices
clr: used to import Dalamud plugins