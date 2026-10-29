# Подключение NuGet-пакета PbSdk

Пакет PbSdk не публикуется на nuget.org. Его кладут файлом в это решение, а зависимости пакета (Serilog, Newtonsoft.Json, Grpc и остальные) восстанавливаются с nuget.org.

## nuget.config

Файл лежит в корне решения, рядом с `PbSdk.sln`. NuGet читает его для всех проектов решения. Без этого файла источник `nupkg` неизвестен, и `PackageReference` на PbSdk не восстановится.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="local-pbsdk" value="nupkg" />
  </packageSources>
</configuration>
```

`nuget.org` нужен для зависимостей, которые PbSdk объявляет в nuspec. `local-pbsdk` — локальная папка с самим пакетом. Путь `nupkg` относительный и считается от папки, где лежит `nuget.config`, то есть от корня решения.

## Папка nupkg

В корне решения, рядом с `nuget.config`. В ней лежат файлы `PbSdk.<версия>.nupkg`. Другие пакеты туда класть не нужно: их NuGet скачает сам.

Новый пакет копируют в эту папку. Имя версии в имени файла и в ссылке проекта должны совпадать. NuGet кэширует пакет по id и версии, поэтому повторная выдача того же номера не подхватит другой nupkg. Перед копированием нового содержимого версию поднимают.

## Ссылка в проекте

В csproj хоста, как в `PbSdkConsole`:

```xml
<ItemGroup>
  <PackageReference Include="PbSdk" Version="0.1.32" />
</ItemGroup>
```

`Version` — номер из имени файла в `nupkg`, без суффикса `.nupkg`. После добавления ссылки или замены пакета — Restore NuGet Packages. Отдельные ссылки на `ICore` и остальные наши dll не нужны: они внутри пакета. Сторонние пакеты из nuspec подтянутся с nuget.org.
