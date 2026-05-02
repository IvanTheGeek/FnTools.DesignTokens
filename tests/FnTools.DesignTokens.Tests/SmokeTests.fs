module FnTools.DesignTokens.Tests.SmokeTests

open Expecto
open FnTools.DesignTokens


let private roundTrip label json =
    testCase label <| fun () ->
        match Format.parse json with
        | Error errs -> failtestf "parse failed:\n%A" errs
        | Ok file ->
            let serialized = Format.serialize file
            match Format.parse serialized with
            | Error errs -> failtestf "round-trip parse failed:\n%A" errs
            | Ok _ -> ()


let allTests =
    testList "Spec examples (smoke)" [

        // ── design-token.md ───────────────────────────────────────────────────

        roundTrip "minimal color token" """
{
  "token name": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [1, 0, 0] }
  }
}"""

        roundTrip "case-sensitive token names" """
{
  "font-size": {
    "$value": { "value": 3, "unit": "rem" },
    "$type": "dimension"
  },
  "FONT-SIZE": {
    "$value": { "value": 16, "unit": "px" },
    "$type": "dimension"
  }
}"""

        roundTrip "token with $description" """
{
  "Button background": {
    "$type": "color",
    "$description": "The background color for buttons in their normal state.",
    "$value": { "colorSpace": "srgb", "components": [0.467, 0.467, 0.467] }
  }
}"""

        roundTrip "token with $extensions" """
{
  "Button background": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [0.467, 0.467, 0.467] },
    "$extensions": {
      "org.example.tool-a": 42,
      "org.example.tool-b": { "turn-up-to-11": true }
    }
  }
}"""

        roundTrip "token with $deprecated (bool and string)" """
{
  "Button background": {
    "$value": { "colorSpace": "srgb", "components": [0.467, 0.467, 0.467], "hex": "#777777" },
    "$type": "color",
    "$deprecated": true
  },
  "Button focus": {
    "$value": { "colorSpace": "srgb", "components": [0.44, 0.753, 1], "hex": "#70c0ff" },
    "$type": "color",
    "$deprecated": "Please use the border style for active buttons instead."
  }
}"""

        // ── groups.md ─────────────────────────────────────────────────────────

        roundTrip "group with $root token" """
{
  "spacing": {
    "$type": "dimension",
    "$description": "Base spacing scale",
    "$root": { "$value": { "value": 16, "unit": "px" } },
    "small": { "$value": { "value": 8, "unit": "px" } },
    "large": { "$value": { "value": 32, "unit": "px" } }
  }
}"""

        roundTrip "complex hierarchical groups with nested $root and $extends" """
{
  "color": {
    "$type": "color",
    "$description": "Complete color system",
    "brand": {
      "$root": { "$value": { "colorSpace": "srgb", "components": [0, 0.4, 0.8], "hex": "#0066cc" } },
      "strong": { "$value": { "colorSpace": "srgb", "components": [0.2, 0.533, 0.867], "hex": "#3388dd" } },
      "subdued": { "$value": { "colorSpace": "srgb", "components": [0, 0.267, 0.6], "hex": "#004499" } }
    },
    "semantic": {
      "$extends": "{color.brand}",
      "success": {
        "$root": { "$value": { "colorSpace": "srgb", "components": [0, 0.8, 0.4], "hex": "#00cc66" } },
        "strong": { "$value": { "colorSpace": "srgb", "components": [0.2, 0.867, 0.533], "hex": "#33dd88" } },
        "subdued": { "$value": { "colorSpace": "srgb", "components": [0, 0.6, 0.267], "hex": "#009944" } }
      },
      "error": {
        "$root": { "$value": { "colorSpace": "srgb", "components": [0.8, 0, 0], "hex": "#cc0000" } },
        "strong": { "$value": { "colorSpace": "srgb", "components": [1, 0.2, 0.2], "hex": "#ff3333" } },
        "subdued": { "$value": { "colorSpace": "srgb", "components": [0.6, 0, 0], "hex": "#990000" } }
      }
    }
  }
}"""

        roundTrip "organized token groups (color + fontFamily)" """
{
  "brand": {
    "color": {
      "$type": "color",
      "acid green": { "$value": { "colorSpace": "srgb", "components": [0, 1, 0.4] } },
      "hot pink":   { "$value": { "colorSpace": "srgb", "components": [1, 0, 1] } }
    },
    "typeface": {
      "$type": "fontFamily",
      "primary":   { "$value": "Comic Sans MS" },
      "secondary": { "$value": "Times New Roman" }
    }
  }
}"""

        roundTrip "flat token structure" """
{
  "brand-color-acid-green": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [0, 1, 0.4] }
  },
  "brand-color-hot-pink": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [1, 0, 1] }
  },
  "brand-typeface-primary": {
    "$value": "Comic Sans MS",
    "$type": "fontFamily"
  },
  "brand-typeface-secondary": {
    "$value": "Times New Roman",
    "$type": "fontFamily"
  }
}"""

        // ── types.md ─────────────────────────────────────────────────────────

        roundTrip "dimension: px and rem" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "spacing-stack-0": { "$value": { "value": 0, "unit": "px" },   "$type": "dimension" },
  "spacing-stack-1": { "$value": { "value": 0.5, "unit": "rem" }, "$type": "dimension" }
}"""

        roundTrip "fontFamily: single string and stack" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "Primary font": { "$value": "Comic Sans MS",                        "$type": "fontFamily" },
  "Body font":    { "$value": ["Helvetica", "Arial", "sans-serif"],   "$type": "fontFamily" }
}"""

        roundTrip "fontWeight: numeric and keyword" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "font-weight-default": { "$value": 350,          "$type": "fontWeight" },
  "font-weight-thick":   { "$value": "extra-bold", "$type": "fontWeight" }
}"""

        roundTrip "duration: ms and s" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "Duration-Quick": { "$value": { "value": 100, "unit": "ms" }, "$type": "duration" },
  "Duration-Long":  { "$value": { "value": 1.5, "unit": "s" },  "$type": "duration" }
}"""

        roundTrip "cubicBezier: accelerate and decelerate" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "Accelerate": { "$value": [0.5, 0, 1, 1],   "$type": "cubicBezier" },
  "Decelerate": { "$value": [0, 0, 0.5, 1],   "$type": "cubicBezier" }
}"""

        roundTrip "number token" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "line-height-large": { "$value": 2.3, "$type": "number" }
}"""

        // ── composite-types.md ───────────────────────────────────────────────

        roundTrip "shadow: single shadow object" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "shadow-token": {
    "$type": "shadow",
    "$value": {
      "color": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.5, "hex": "#000000" },
      "offsetX": { "value": 0.5, "unit": "rem" },
      "offsetY": { "value": 0.5, "unit": "rem" },
      "blur":    { "value": 1.5, "unit": "rem" },
      "spread":  { "value": 0,   "unit": "rem" }
    }
  }
}"""

        roundTrip "shadow: layered array and inset" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "layered-shadow": {
    "$type": "shadow",
    "$value": [
      {
        "color": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.1 },
        "offsetX": { "value": 0, "unit": "px" }, "offsetY": { "value": 24, "unit": "px" },
        "blur":    { "value": 22, "unit": "px" }, "spread": { "value": 0,  "unit": "px" }
      },
      {
        "color": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.2 },
        "offsetX": { "value": 0, "unit": "px" }, "offsetY": { "value": 42.9, "unit": "px" },
        "blur":    { "value": 44, "unit": "px" }, "spread": { "value": 0,   "unit": "px" }
      }
    ]
  },
  "inner-shadow": {
    "$type": "shadow",
    "$value": {
      "color": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.5 },
      "offsetX": { "value": 2, "unit": "px" }, "offsetY": { "value": 2, "unit": "px" },
      "blur":    { "value": 4, "unit": "px" }, "spread": { "value": 0, "unit": "px" },
      "inset": true
    }
  }
}"""

        roundTrip "shadow: composite token with aliases as sub-values" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "space": {
    "small": { "$type": "dimension", "$value": { "value": 0.5, "unit": "rem" } }
  },
  "color": {
    "shadow-050": {
      "$type": "color",
      "$value": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.5, "hex": "#000000" }
    }
  },
  "shadow": {
    "medium": {
      "$type": "shadow",
      "$description": "composite shadow with sub-value aliases",
      "$value": {
        "color":   "{color.shadow-050}",
        "offsetX": "{space.small}",
        "offsetY": "{space.small}",
        "blur":    { "value": 1.5, "unit": "rem" },
        "spread":  { "value": 0,   "unit": "rem" }
      }
    }
  },
  "component": {
    "card": {
      "box-shadow": {
        "$description": "alias for the composite shadow token",
        "$value": "{shadow.medium}"
      }
    }
  }
}"""

        roundTrip "strokeStyle: keyword" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "focus-ring-style": { "$type": "strokeStyle", "$value": "dashed" }
}"""

        roundTrip "strokeStyle: object with dashArray and lineCap" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "alert-border-style": {
    "$type": "strokeStyle",
    "$value": {
      "dashArray": [
        { "value": 0.5,  "unit": "rem" },
        { "value": 0.25, "unit": "rem" }
      ],
      "lineCap": "round"
    }
  }
}"""

        roundTrip "strokeStyle: dashArray with dimension aliases" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "notification-border-style": {
    "$type": "strokeStyle",
    "$value": {
      "dashArray": ["{dash-length-medium}", { "value": 0.25, "unit": "rem" }],
      "lineCap": "butt"
    }
  },
  "dash-length-medium": { "$type": "dimension", "$value": { "value": 10, "unit": "px" } },
  "dash-length-long":   { "$type": "dimension", "$value": { "value": 1,  "unit": "rem" } }
}"""

        roundTrip "border: explicit and with strokeStyle object" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "border": {
    "heavy": {
      "$type": "border",
      "$value": {
        "color": { "colorSpace": "srgb", "components": [0.218, 0.218, 0.218] },
        "width": { "value": 3, "unit": "px" },
        "style": "solid"
      }
    },
    "focusring": {
      "$type": "border",
      "$value": {
        "color": "{color.focusring}",
        "width": { "value": 1, "unit": "px" },
        "style": {
          "dashArray": [
            { "value": 0.5,  "unit": "rem" },
            { "value": 0.25, "unit": "rem" }
          ],
          "lineCap": "round"
        }
      }
    }
  }
}"""

        roundTrip "transition" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "transition": {
    "emphasis": {
      "$type": "transition",
      "$value": {
        "duration":       { "value": 200, "unit": "ms" },
        "delay":          { "value": 0,   "unit": "ms" },
        "timingFunction": [0.5, 0, 1, 1]
      }
    }
  }
}"""

        roundTrip "gradient: two-stop and with position alias" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "blue-to-red": {
    "$type": "gradient",
    "$value": [
      { "color": { "colorSpace": "srgb", "components": [0, 0, 1] }, "position": 0 },
      { "color": { "colorSpace": "srgb", "components": [1, 0, 0] }, "position": 1 }
    ]
  },
  "position-end": { "$type": "number", "$value": 1 },
  "brand-primary": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [0, 1, 0.4] }
  },
  "brand-in-the-middle": {
    "$type": "gradient",
    "$value": [
      { "color": { "colorSpace": "srgb", "components": [0, 0, 0] }, "position": 0 },
      { "color": "{brand-primary}", "position": 0.5 },
      { "color": { "colorSpace": "srgb", "components": [0, 0, 0] }, "position": "{position-end}" }
    ]
  }
}"""

        roundTrip "typography: explicit values and token aliases" """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "type styles": {
    "heading-level-1": {
      "$type": "typography",
      "$value": {
        "fontFamily":    "Roboto",
        "fontSize":      { "value": 42,  "unit": "px" },
        "fontWeight":    700,
        "letterSpacing": { "value": 0.1, "unit": "px" },
        "lineHeight":    1.2
      }
    },
    "microcopy": {
      "$type": "typography",
      "$value": {
        "fontFamily":    "{font.serif}",
        "fontSize":      "{font.size.smallest}",
        "fontWeight":    "{font.weight.normal}",
        "letterSpacing": { "value": 0, "unit": "px" },
        "lineHeight":    1
      }
    }
  }
}"""

        // ── aliases.md ───────────────────────────────────────────────────────

        roundTrip "alias: curly-brace reference" """
{
  "colors": {
    "blue": {
      "$value": { "colorSpace": "srgb", "components": [0, 0.4, 0.8], "hex": "#0066cc" },
      "$type": "color"
    }
  },
  "semantic": {
    "primary": {
      "$value": "{colors.blue}",
      "$type": "color"
    }
  }
}"""

    ]
