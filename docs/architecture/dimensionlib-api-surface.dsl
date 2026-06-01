workspace "DimensionLib API Surface" "C4 model for DimensionLib and the Pocket Dimensions consumer mod." {
    model {
        serverAdmin = person "Server Admin / QA" "Runs root/admin commands to create, inspect, enter, and release dimensions."
        modAuthor = person "Mod Author" "Builds mods that consume DimensionLib."
        player = person "Player" "Enters prepared dimensions through commands or future gameplay devices."

        vintageStory = softwareSystem "Vintage Story Runtime" "The game runtime, world storage, mod loader, APIs, networking, and rendering pipeline." {
            tags "External"

            vsServerApi = container "Server API" "ICoreServerAPI, chat commands, privileges, world manager, block accessor, and events." "Vintage Story API" {
                tags "External"
            }

            vsClientApi = container "Client API" "ICoreClientAPI, render stages, ambient manager, shaders, and client network channel." "Vintage Story API" {
                tags "External"
            }

            vsWorldStorage = container "World Storage" "Saved chunks, ModData, ModConfig, and loaded chunk columns." "Vintage Story world save" {
                tags "External,Database"
            }

            vsAssetSystem = container "Asset System" "Loads mod assets such as blocktypes, textures, shaders, and language files." "Vintage Story assets" {
                tags "External"
            }
        }

        dimensionLib = softwareSystem "DimensionLib" "Library mod that registers finite alternate dimensions, prepares chunks, syncs transfers, and applies dimension visuals." {
            publicApi = container "Public API" "Consumer-facing C# API surface in DimensionLib.Api." "C# API" {
                tags "API"

                idimensionLibApi = component "IDimensionLibApi" "Server-side facade for registration, lookup, preparation, validation, force-send, teleport, return, release, policy providers, and generators." "C# interface" {
                    tags "API"
                }

                dimensionSpec = component "DimensionSpec / Dimension" "Public dimension declaration and immutable registered dimension metadata. Normal specs auto-allocate sparse backing chunks." "C# records/classes" {
                    tags "API,Model"
                }

                placementModel = component "DimensionPlacement" "AutomaticSparse default with Explicit for debug/fixed backing layouts." "C# enum" {
                    tags "API,Model"
                }

                resultTypes = component "DimensionLibResult" "Non-throwing success/failure result objects for public operations." "C# class" {
                    tags "API,Model"
                }

                blockSourceApi = component "IBlockVolumeSource / IChunkColumnWriter" "Materialization seam: consumer sources fill local chunk columns through a writer." "C# interfaces" {
                    tags "API,Extension Point"
                }

                generatorApi = component "IDimensionGenerator" "Generator seam for mods that create block sources from a dimension seed/profile." "C# interface" {
                    tags "API,Extension Point"
                }

                policyApi = component "IDimensionPolicyProvider" "Owner-mod access and mutation policy seam used by OwnerOnly dimensions." "C# interface" {
                    tags "API,Extension Point"
                }

                visualIds = component "DimensionVisualProfileIds" "Public visual profile identifiers including debug, opposite-day, nether-cavern, and pocket-void." "C# constants" {
                    tags "API,Visual"
                }
            }

            serverRuntime = container "Server Runtime" "DimensionLib server-side orchestration and persistence." "C# ModSystem/services" {
                tags "Core"

                dimensionLibModSystem = component "DimensionLibModSystem" "ModSystem and public API facade; stores IDimensionLibApi in ObjectCache and delegates server calls." "C# ModSystem" {
                    tags "Core"
                }

                serverService = component "DimensionLibServerService" "Main server orchestrator for registry, preparation, generation, transfer, release, and diagnostics." "C# service" {
                    tags "Core"
                }

                registry = component "DimensionRegistry" "Owns registered dimensions, orphaned state, lookup by id, and lookup by block position." "C# service" {
                    tags "Core"
                }

                sparseAllocator = component "DimensionRegionAllocator" "Assigns far-apart sparse backing chunk rectangles for normal new dimensions." "C# service" {
                    tags "Core"
                }

                specValidator = component "DimensionSpecValidator" "Validates ids, owners, dimensions, bounds, overlap, and idempotent claim refresh." "C# service" {
                    tags "Core"
                }

                manifestService = component "DimensionManifestService" "Persists registered dimensions, orphan state, and prepared chunk keys." "JSON manifest" {
                    tags "Persistence"
                }

                chunkService = component "DimensionChunkService" "Creates, loads, relights, clears, and force-sends backing chunk columns." "Vintage Story chunk APIs" {
                    tags "Core"
                }

                materializer = component "ChunkColumnMaterializer" "Calls IBlockVolumeSource.FillColumn for each local chunk column." "C# service" {
                    tags "Core"
                }

                chunkWriter = component "BlockAccessorChunkColumnWriter" "Writes local source coordinates into backing world coordinates." "C# adapter" {
                    tags "Core"
                }

                transferService = component "DimensionTransferService" "Moves players, force-sends chunks, and syncs client visual profile data." "C# service/network" {
                    tags "Core"
                }

                returnStore = component "ReturnPositionStore" "Stores last prototype return target for ReturnPlayer." "C# service" {
                    tags "Core"
                }

                policyRegistry = component "PolicyProviderRegistry" "Stores owner-mod policy providers." "C# service" {
                    tags "Core"
                }

                accessService = component "DimensionAccessService" "Combines core read-only/orphan/admin rules with owner policy callbacks." "C# service" {
                    tags "Core"
                }

                protectionAdapter = component "BlockInteractionProtectionAdapter" "Vintage Story event adapter for block place/break/use protection." "C# adapter" {
                    tags "Core"
                }

                generatorRegistry = component "DimensionGeneratorRegistry" "Stores IDimensionGenerator implementations and generator ids." "C# service" {
                    tags "Core,Experimental"
                }

                generatedPreparer = component "GeneratedDimensionWindowPreparer / Streamer" "Prepares generated dimensions in windows and lazily streams nearby generated columns." "C# service" {
                    tags "Core,Experimental"
                }

                lightPolicy = component "DimensionLightPolicy / ChunkLightFloorApplier" "Experimental sealed-space baked light floor policy and application." "C# service" {
                    tags "Experimental"
                }

                diagnostics = component "DimensionDiagnosticService" "Validates metadata, source/generator bounds, prepared state, and spawn samples." "C# service" {
                    tags "Core"
                }

                dlibCommands = component "/dlib Commands" "Root-only debug, diagnostic, QA, and maintenance command tree." "Vintage Story chat commands" {
                    tags "Command,Experimental"
                }

                builtInFixtures = component "Built-in Test Fixtures" "Debug spike, overworld-opposite, and nether-cavern stress fixtures." "C# generators/assets" {
                    tags "Experimental"
                }
            }

            clientVisuals = container "Client Visuals" "Client-side visual profile application, sky cover, and minimum scene light overlay." "C# renderers/network" {
                tags "Visual"

                visualSystem = component "DimensionVisualSystem" "Receives transfer/tuning messages, tracks active profile, and coordinates visual renderers." "C# renderer" {
                    tags "Visual"
                }

                profileRegistry = component "VisualProfileRegistry" "Defines built-in visual profile ambient/fog/sky/lift defaults." "C# registry" {
                    tags "Visual"
                }

                ambientController = component "AmbientModifierController" "Applies/removes Vintage Story AmbientModifier entries while inside a dimension." "C# service" {
                    tags "Visual"
                }

                overlayRenderer = component "ScreenColorOverlayRenderer" "Draws opaque sky cover and after-final-composition minimum-light lift." "C# renderer/shader" {
                    tags "Visual"
                }

                vanillaSuppressor = component "VanillaEffectSuppressor" "Suppresses inherited temporal/cave-fog effects for active DimensionLib profiles." "C# service" {
                    tags "Visual"
                }

                visualTuning = component "VisualTuningState / VisualTuningBroadcaster" "Root-only live tuning path for visual experiments." "C# debug tooling" {
                    tags "Visual,Experimental"
                }
            }
        }

        pocketDimensions = softwareSystem "Pocket Dimensions" "Admin-managed pocket dimension product mod and first DimensionLib consumer." {
            pocketRuntime = container "Pocket Dimensions Mod" "Command, config, policy, and block-source implementation." "C# ModSystem" {
                tags "Consumer"

                pocketModSystem = component "PocketDimensionModSystem" "Loads config/Waystone links, registers policy provider, registers /pocket commands, creates/enters/exits/releases pockets." "C# ModSystem" {
                    tags "Consumer"
                }

                pocketCommands = component "/pocket Commands" "Config-privileged create, enter, exit, list, inspect, bind, unbind, and release commands." "Vintage Story chat commands" {
                    tags "Command,Consumer"
                }

                pocketConfig = component "pocket_dimensions.json" "Create/enter/release privileges, default size, max size, and default spawn Y." "ModConfig JSON" {
                    tags "Config,Consumer"
                }

                pocketLinkStore = component "PocketLinkStore" "Persists external Waystone endpoint links and minimal active ingress state for return pedestal recovery across restarts." "JSON ModData store" {
                    tags "Persistence,Consumer"
                }

                pocketPolicy = component "Pocket Policy Provider" "IDimensionPolicyProvider implementation requiring the configured enter privilege and vetoing managed floor/pedestal breaks." "C# policy" {
                    tags "Consumer,Extension Point"
                }

                pocketSource = component "PocketPlatformSource" "IBlockVolumeSource that fills every floor block and places the center-adjacent return pedestal." "C# block source" {
                    tags "Consumer,Extension Point"
                }

                pocketWaystone = component "Pocket Waystone" "Craftable bindable ingress block that records its endpoint id and enters a target pocket." "C# block/assets" {
                    tags "Consumer,Asset"
                }

                pocketReturnPedestal = component "Pocket Return Pedestal" "Generated protected return block that teleports to the active linked Waystone endpoint for the current pocket." "C# block/assets" {
                    tags "Consumer,Asset"
                }

                pocketAssets = component "Pocket Assets" "Matte black grid floor, Pocket Waystone, Pocket Return Pedestal, texture, and lang assets." "Vintage Story assets" {
                    tags "Asset,Consumer"
                }
            }
        }

        futureConsumer = softwareSystem "Future Consumer Mods" "Examples: Mystcraft-like ages, Nether-like dimensions, replay previews, portal/device mods, and worldgen integrations." {
            tags "External,Consumer"
        }

        modAuthor -> idimensionLibApi "Reads and calls public API" "C#"
        modAuthor -> pocketRuntime "Uses as copyable consumer example" "C#"
        serverAdmin -> pocketCommands "Runs pocket management commands" "Vintage Story chat/console"
        player -> pocketCommands "Uses enter/exit when allowed" "Vintage Story chat"
        player -> pocketWaystone "Right-clicks bound external Waystones" "Vintage Story block interaction"
        player -> pocketReturnPedestal "Right-clicks generated return pedestal" "Vintage Story block interaction"

        futureConsumer -> idimensionLibApi "Registers dimensions, policies, generators, and block sources" "C#"

        pocketCommands -> pocketModSystem "Dispatches commands to"
        pocketModSystem -> pocketConfig "Loads and normalizes" "LoadModConfig/StoreModConfig"
        pocketModSystem -> pocketLinkStore "Loads, saves, and clears Waystone links and active ingress choices"
        pocketLinkStore -> vsWorldStorage "Stores ModData/pocketdimensions/waystone-links.json"
        pocketModSystem -> idimensionLibApi "Registers policy provider; creates, prepares, teleports, lists, and releases dimensions" "C# calls"
        pocketPolicy -> policyApi "Implements"
        pocketModSystem -> pocketPolicy "Registers as owner policy provider"
        pocketSource -> blockSourceApi "Implements"
        pocketModSystem -> pocketSource "Uses to materialize floor and return pedestal"
        pocketSource -> pocketReturnPedestal "Places center-adjacent return block"
        pocketWaystone -> pocketModSystem "Requests bound-pocket entry" "C# calls"
        pocketReturnPedestal -> pocketModSystem "Requests return to active linked endpoint" "C# calls"
        pocketModSystem -> idimensionLibApi "Calls TeleportToDimension and TeleportToLocation" "C# calls"
        pocketPolicy -> pocketReturnPedestal "Prevents player breaks"
        pocketAssets -> vsAssetSystem "Provides blocktypes, lang, and texture assets"

        idimensionLibApi -> dimensionLibModSystem "Implemented by"
        dimensionLibModSystem -> serverService "Delegates server operations to"
        dimensionLibModSystem -> vsServerApi "Registers channels and command tree through"
        dimensionLibModSystem -> vsClientApi "Registers client renderers and network handlers through"

        serverService -> registry "Registers, refreshes, looks up, and releases dimensions"
        serverService -> sparseAllocator "Auto-assigns sparse backing chunks for AutomaticSparse specs"
        serverService -> specValidator "Validates specs and overlap rules"
        serverService -> manifestService "Loads and saves manifest entries"
        manifestService -> vsWorldStorage "Stores ModData/dimensionlib/regions.json"
        serverService -> chunkService "Creates, relights, clears, loads, and force-sends chunks"
        chunkService -> vsServerApi "Uses world manager and chunk APIs"
        chunkService -> vsWorldStorage "Reads/writes chunk columns"
        serverService -> materializer "Materializes consumer block sources"
        materializer -> blockSourceApi "Calls FillColumn"
        materializer -> chunkWriter "Writes through"
        chunkWriter -> vsServerApi "Uses block accessor"
        serverService -> transferService "Teleports players and syncs clients"
        transferService -> returnStore "Records and resolves return positions"
        transferService -> vsServerApi "Moves players and sends packets"
        transferService -> clientVisuals "Sends DimensionTransferMessage with visual profile and light data" "network channel"
        transferService -> visualSystem "Sends DimensionTransferMessage with visual profile and light data" "network channel"
        serverService -> policyRegistry "Registers owner policy providers"
        serverService -> accessService "Checks enter/use/mutate access"
        accessService -> policyRegistry "Finds owner provider"
        accessService -> policyApi "Calls CanEnter/CanUseBlock/CanMutateBlock"
        protectionAdapter -> accessService "Applies decisions to block events"
        protectionAdapter -> vsServerApi "Subscribes to place/break/use events"
        serverService -> generatorRegistry "Registers and resolves generators"
        generatorRegistry -> generatorApi "Stores implementations of"
        serverService -> generatedPreparer "Prepares generated chunk windows"
        generatedPreparer -> lightPolicy "Applies experimental sealed-space light floors"
        serverService -> diagnostics "Builds validation reports"
        dlibCommands -> serverService "Invokes debug and maintenance operations"
        builtInFixtures -> generatorApi "Implements internal test generators"

        clientVisuals -> vsClientApi "Uses render stages, ambient manager, shaders, and client world visibility"
        visualSystem -> profileRegistry "Resolves profile defaults"
        visualSystem -> ambientController "Applies profile ambient state"
        visualSystem -> overlayRenderer "Renders sky cover and minimum-light lift"
        visualSystem -> vanillaSuppressor "Suppresses inherited vanilla effects"
        visualTuning -> visualSystem "Updates debug tuning state"
    }

    views {
        systemLandscape "DimensionLibLandscape" "DimensionLib, Pocket Dimensions, and external actors/systems." {
            include serverAdmin modAuthor player vintageStory dimensionLib pocketDimensions futureConsumer
            autoLayout lr
        }

        container dimensionLib "DimensionLibContainers" "DimensionLib containers and direct consumers." {
            include serverAdmin modAuthor player vsServerApi vsClientApi vsWorldStorage pocketRuntime futureConsumer publicApi serverRuntime clientVisuals
            autoLayout lr
        }

        component publicApi "DimensionLibPublicApi" "Public API surface exposed to consumer mods." {
            include *
            autoLayout lr
        }

        component serverRuntime "DimensionLibServerRuntime" "Server-side API implementation and persistence/transfer/protection services." {
            include *
            autoLayout lr
        }

        component clientVisuals "DimensionLibClientVisuals" "Client visual profile application surface." {
            include *
            autoLayout lr
        }

        component pocketRuntime "PocketDimensionsComponents" "Pocket Dimensions consumer API usage and product surface." {
            include *
            autoLayout lr
        }

        dynamic pocketRuntime "CreatePocketFlow" "Create and prepare a new pocket dimension." {
            serverAdmin -> pocketCommands "Runs /pocket create qa-v01 3"
            pocketCommands -> pocketModSystem "Dispatches create"
            pocketModSystem -> pocketConfig "Reads privileges/defaults"
            pocketModSystem -> idimensionLibApi "RegisterDimension(spec)"
            idimensionLibApi -> dimensionLibModSystem "API facade call"
            dimensionLibModSystem -> serverService "RegisterDimension"
            serverService -> sparseAllocator "Assign sparse backing chunks"
            serverService -> registry "Store registered Dimension"
            serverService -> manifestService "Persist manifest"
            pocketModSystem -> pocketSource "Create floor and return pedestal source"
            pocketModSystem -> idimensionLibApi "PrepareDimension(source)"
            serverService -> materializer "Materialize source"
            materializer -> blockSourceApi "FillColumn"
            materializer -> chunkWriter "SetBlock local floor and return pedestal cells"
            chunkWriter -> vsServerApi "Write backing blocks"
            serverService -> chunkService "Relight and mark prepared"
            autoLayout lr
        }

        dynamic pocketRuntime "EnterPocketFlow" "Enter and return from a prepared pocket dimension." {
            player -> pocketWaystone "Right-clicks bound external Waystone"
            pocketWaystone -> pocketModSystem "EnterBoundPocket"
            pocketModSystem -> pocketLinkStore "Resolve or update endpoint link and record active ingress"
            pocketModSystem -> idimensionLibApi "TeleportToDimension"
            idimensionLibApi -> dimensionLibModSystem "API facade call"
            dimensionLibModSystem -> serverService "TeleportToDimension"
            serverService -> accessService "CanEnter"
            accessService -> policyApi "Owner policy check"
            serverService -> chunkService "Force-send prepared columns"
            serverService -> transferService "Move player and sync profile"
            transferService -> visualSystem "DimensionTransferMessage"
            visualSystem -> ambientController "Apply pocket-void ambient"
            visualSystem -> overlayRenderer "Render static dark sky and light lift"
            player -> pocketReturnPedestal "Right-clicks return pedestal"
            pocketReturnPedestal -> pocketModSystem "ReturnFromPocket"
            pocketModSystem -> pocketLinkStore "Resolve active ingress endpoint"
            pocketModSystem -> idimensionLibApi "TeleportToLocation"
            serverService -> transferService "Move player to linked Waystone location"
            pocketModSystem -> pocketLinkStore "Clear active ingress"
            autoLayout lr
        }

        styles {
            element "Person" {
                shape Person
                background #08427b
                color #ffffff
            }

            element "External" {
                background #999999
                color #ffffff
            }

            element "API" {
                background #1168bd
                color #ffffff
            }

            element "Core" {
                background #438dd5
                color #ffffff
            }

            element "Consumer" {
                background #6a3d9a
                color #ffffff
            }

            element "Visual" {
                background #00897b
                color #ffffff
            }

            element "Persistence" {
                shape Cylinder
                background #f5a623
                color #000000
            }

            element "Database" {
                shape Cylinder
            }

            element "Command" {
                shape Terminal
            }

            element "Config" {
                shape Folder
                background #f5a623
                color #000000
            }

            element "Asset" {
                shape Folder
                background #7cb342
                color #ffffff
            }

            element "Experimental" {
                border Dashed
                opacity 75
            }

            relationship "Relationship" {
                thickness 2
                color #707070
            }
        }
    }
}
