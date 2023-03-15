Does what it says on the tin
# Universalis Price Checker

Universalis Price Checker is a plugin for Final Fantasy XIV that utilizes the Dalamud framework. This plugin allows players to quickly check the minimum and maximum prices for items on different servers using the Universalis market API.

## Features

- Check item prices with a right-click context menu in the inventory
- Search for item prices using a plugin UI window
- Customizable minimum and maximum prices for each server

## Manual Installation

To install the Universalis Price Checker plugin, follow these steps:

1. Download the latest release from the [GitHub Releases](https://github.com/LTS-FFXIV/UniversalisPriceChecker/releases) page.
2. Extract the downloaded ZIP file.
3. Copy the extracted `UniversalisPriceChecker.dll` file to the `plugins` folder in your Dalamud installation directory (usually located in `%AppData%\XIVLauncher\installedPlugins`).
4. Launch Final Fantasy XIV and log in to your character.
5. Open the Dalamud plugin installer (`/xlplugins` command) and enable the Universalis Price Checker plugin.

## Usage

After enabling the plugin, you can check the prices of items in your inventory by right-clicking the item and selecting "Check Universalis Prices" from the context menu.

To search for an item using the plugin UI, use the `/upc` command followed by the item name. For example:

/upc Skybuilders' Alloy


This command will fetch and display the minimum and maximum prices for the specified item across the selected servers.

To customize the minimum and maximum prices for each server, open the plugin settings by right-clicking the plugin in the Dalamud plugin installer and selecting "Settings."

# Planned Features
Adding functionality to see the most commonly purchased items over the last few days.
Other stuff idk

## Contributing

Contributions to the Universalis Price Checker plugin are welcome! If you'd like to contribute, please create a fork of the repository, make your changes, and submit a pull request.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
