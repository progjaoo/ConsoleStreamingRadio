# Handoff para Codex no PC Windows

Este arquivo resume o estado atual do projeto para continuar o trabalho em outro computador.

## Objetivo do sistema

Aplicacao console C#/.NET que recebe stream de radio, toca em uma placa de audio escolhida, usa buffer, faz failover entre 3 links e pode rodar como servico Windows via WinSW.

## Estado atual

Implementado e compilando:

- Menu console.
- Configuracao JSON.
- Selecao de placa de audio.
- Buffer de audio com `BufferedWaveProvider`.
- Failover entre principal/reserva 1/reserva 2.
- Retorno automatico ao principal.
- Logs e exportacao TXT.
- Config WinSW.

## Arquivos mais importantes

- `StreamPlayerService.cs`: logica principal de audio, buffer, failover e retorno ao principal.
- `AudioDeviceService.cs`: backends `DirectSound`, `Wasapi`, `WaveOut`.
- `ConsoleMenu.cs`: opcoes administrativas.
- `ConfigService.cs`: normalizacao/validacao de `radio-config.json`.
- `AppConfig.cs`: modelos da config.
- `AppLogger.cs`: logs.
- `winsw/GTF-RX-Tlink-Service.xml`: wrapper WinSW.

## Comandos de validacao

```powershell
dotnet restore
dotnet build "RadioStreamPlayer.sln"
dotnet publish "GTF RX Tlink.csproj" -c Release -o ".\publish"
```

Teste local:

```powershell
cd .\publish
.\GTF RX Tlink.exe --console
```

## WinSW

Na pasta `publish`, colocar:

```text
GTF-RX-Tlink-Service.exe
GTF-RX-Tlink-Service.xml
```

O build/publish copia o XML e gera `GTF-RX-Tlink-Service.exe` automaticamente a
partir do executavel do pacote WinSW. O `.xml` fonte esta em
`winsw/GTF-RX-Tlink-Service.xml`.

Instalar:

```powershell
.\GTF-RX-Tlink-Service.exe install
.\GTF-RX-Tlink-Service.exe start
```

## Pontos de atencao

- O app toca audio em console, mas servico Windows pode ter limitacoes de sessao.
- Se nao sair audio como servico, configurar conta do servico para um usuario local com acesso a placa.
- O XML do WinSW chama o app com `--run`.
- Quando `GTF-RX-Tlink-Service.exe` existe ao lado do app, os comandos
  `--install-service`, `--start-service`, `--stop-service`, `--restart-service`,
  `--uninstall-service` e `--status-service` usam WinSW.
- Nao usar menu interativo dentro de servico.
- `--service` ainda existe no codigo antigo, mas a estrategia recomendada agora e WinSW.

## Diagnostico

Logs do app:

```text
logs\radio-stream-player.log
```

Exportar logs:

```powershell
.\GTF RX Tlink.exe --export-logs
```

Se o failover nao trocar:

- Verificar `Stream.Links` no JSON.
- Conferir `Enabled: true` nas reservas.
- Conferir se o link realmente para de enviar bytes.
- Ajustar `BufferUnderrunFailSeconds`.

Se demora para voltar ao principal:

- Ajustar `PrimaryRetrySeconds`.
- Verificar se o principal esta respondendo HTTP com bytes.
