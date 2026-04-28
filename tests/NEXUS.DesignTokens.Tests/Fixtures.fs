module NEXUS.DesignTokens.Tests.Fixtures

open NEXUS.DesignTokens


// ─── Helpers ─────────────────────────────────────────────────────────────────

let tn (s: string) =
    match TokenName.tryCreate s with
    | Ok n -> n
    | Error msg -> failwithf "fixture token name '%s' invalid: %s" s msg

let emptyMeta : Metadata =
    { Description = None; Deprecated = None; Extensions = [] }


// ─── V2025_10 simple tokens ──────────────────────────────────────────────────

module V2025_10 =

    let colorBrandJson = """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "color": {
    "$type": "color",
    "brand": {
      "$value": {
        "colorSpace": "srgb",
        "components": [1, 0.5, 0],
        "alpha": 1,
        "hex": "#ff8000"
      }
    }
  }
}
"""

    let colorBrandValue : ColorValue =
        { ColorSpace = SRGB
          Components = (Channel 1.0, Channel 0.5, Channel 0.0)
          Alpha = Some 1.0
          Hex = Some "#ff8000" }

    let dimensionJson = """
{
  "spacing": {
    "$type": "dimension",
    "small": {
      "$value": { "value": 8, "unit": "px" }
    }
  }
}
"""

    let dimensionValue : DimensionValue = { Value = 8.0; Unit = Px }

    let aliasJson = """
{
  "color": {
    "$type": "color",
    "brand": {
      "$value": {
        "colorSpace": "srgb",
        "components": [1, 0, 0]
      }
    },
    "primary": {
      "$value": "{color.brand}"
    }
  }
}
"""

    let typographyJson = """
{
  "type": {
    "heading": {
      "$type": "typography",
      "$value": {
        "fontFamily": "Inter",
        "fontSize":   { "value": 24, "unit": "px" },
        "fontWeight": 700,
        "letterSpacing": { "value": 0, "unit": "px" },
        "lineHeight": 1.5
      }
    }
  }
}
"""

    let gradientJson = """
{
  "fade": {
    "$type": "gradient",
    "$value": [
      { "color": { "colorSpace": "srgb", "components": [1, 0, 0] }, "position": 0 },
      { "color": { "colorSpace": "srgb", "components": [0, 0, 1] }, "position": 1 }
    ]
  }
}
"""

    let shadowSingleJson = """
{
  "elevation": {
    "$type": "shadow",
    "card": {
      "$value": {
        "color":   { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.25 },
        "offsetX": { "value": 0, "unit": "px" },
        "offsetY": { "value": 2, "unit": "px" },
        "blur":    { "value": 4, "unit": "px" },
        "spread":  { "value": 0, "unit": "px" },
        "inset":   false
      }
    }
  }
}
"""

    let strokeStyleKeywordJson = """
{
  "border": {
    "$type": "strokeStyle",
    "default": { "$value": "solid" }
  }
}
"""

    let extensionsJson = """
{
  "color": {
    "$type": "color",
    "brand": {
      "$value": { "colorSpace": "srgb", "components": [1, 0, 0] },
      "$extensions": {
        "com.figma": { "nodeId": "1:23" }
      }
    }
  }
}
"""


// ─── Older versions (for upgrade-path tests) ────────────────────────────────

module FirstED =
    // First Editors' Draft (2021): unprefixed keys, hex string colors
    let colorJson = """
{
  "color": {
    "type": "color",
    "brand": {
      "value": "#ff0000"
    }
  }
}
"""

    let dimensionJson = """
{
  "spacing": {
    "type": "dimension",
    "small": { "value": "8px" }
  }
}
"""


module SecondED =
    // Second Editors' Draft (2022): $type/$value, but still hex strings
    let colorJson = """
{
  "color": {
    "$type": "color",
    "brand": { "$value": "#0000ff" }
  }
}
"""

    let dimensionStringJson = """
{
  "spacing": {
    "$type": "dimension",
    "small": { "$value": "16px" }
  }
}
"""


module ThirdED =
    // Third Editors' Draft (2025-08): color object, dimension object — no resolver
    let colorJson = """
{
  "color": {
    "$type": "color",
    "brand": {
      "$value": {
        "colorSpace": "srgb",
        "components": [0.5, 0.5, 0.5]
      }
    }
  }
}
"""


// ─── Resolver fixtures ──────────────────────────────────────────────────────

module Resolver =

    let basicResolverJson = """
{
  "version": "2025.10",
  "name": "demo",
  "sets": {
    "core": {
      "sources": [
        { "inline": {
            "color": {
              "$type": "color",
              "brand": { "$value": { "colorSpace": "srgb", "components": [1, 0, 0] } }
            }
          }
        }
      ]
    }
  },
  "modifiers": {
    "theme": {
      "default": "light",
      "contexts": {
        "light": {
          "sources": [
            { "inline": {
                "color": {
                  "$type": "color",
                  "background": { "$value": { "colorSpace": "srgb", "components": [1, 1, 1] } }
                }
              }
            }
          ]
        },
        "dark": {
          "sources": [
            { "inline": {
                "color": {
                  "$type": "color",
                  "background": { "$value": { "colorSpace": "srgb", "components": [0, 0, 0] } }
                }
              }
            }
          ]
        }
      }
    }
  },
  "resolutionOrder": [
    { "set": "core" },
    { "modifier": "theme" }
  ]
}
"""


// ─── Invalid fixtures (rejection tests) ──────────────────────────────────────

module Invalid =

    let dollarPrefixName = """
{
  "$bad": { "$type": "color", "$value": { "colorSpace": "srgb", "components": [0, 0, 0] } }
}
"""

    let unknownTokenType = """
{
  "x": { "$type": "magic", "$value": 1 }
}
"""

    let alphaOutOfRange = """
{
  "c": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 1.5 }
  }
}
"""

    let cubicBezierOutOfRange = """
{
  "ease": {
    "$type": "cubicBezier",
    "$value": [1.5, 0, 0.5, 1]
  }
}
"""

    let gradientTooFew = """
{
  "g": {
    "$type": "gradient",
    "$value": [
      { "color": { "colorSpace": "srgb", "components": [1, 0, 0] }, "position": 0 }
    ]
  }
}
"""

    let circularAlias = """
{
  "a": { "$type": "color", "$value": "{b}" },
  "b": { "$type": "color", "$value": "{a}" }
}
"""
