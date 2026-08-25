# Game Service Implementations

This folder contains the generic service contracts, reusable base classes and concrete platform services.

## Service Families

- `Generic` - interfaces and service ids.
- `Base` - shared base implementations for services and modules.
- `EditorService` - local testing service inside Unity Editor.
- `DesktopService` - default native PC fallback service with guest player support.
- `ItchService` - Itch desktop token/profile integration and future Web path.
- `VKPlayService` - VK Play service integration.
- `VkGamesService` - VK Games WebGL launch, identity, storage and platform integration.
- `YandexGamesService` - Yandex Games WebGL SDK integration.

## Rules

- Unsupported feature modules should still finish initialization and report `IsSupported = false`.
- `IPlayerModule.IsAuthenticationSupported` means external platform authentication, not regular MST login.
- Keep guest fallback behavior available for desktop/plain-web paths.
- Service modules should not directly mutate MST auth/profile state; use the normal auth/client flow.
