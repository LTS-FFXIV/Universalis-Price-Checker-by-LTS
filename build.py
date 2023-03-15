import os
import zipfile

plugin_file = "universalis_price_checker.py"
output_zip = "UniversalisPriceChecker.zip"

with zipfile.ZipFile(output_zip, 'w', zipfile.ZIP_DEFLATED) as zipf:
    zipf.write(plugin_file, os.path.basename(plugin_file))

print(f"Plugin packaged as {output_zip}")
