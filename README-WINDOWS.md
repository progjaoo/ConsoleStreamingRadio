# GTF RX Tlink - uso no Windows

## Configuracao

O arquivo `radio-config.json` fica ao lado do EXE publicado. Nele voce pode ajustar:

- `Stream.Url`: URL do streaming.
- `Stream.Links`: ate 3 links de transmissao, sendo o primeiro sempre o principal.
- `Stream.BufferSeconds`: tamanho total do buffer de audio.
- `Stream.PrebufferSeconds`: quanto o player deve carregar antes de iniciar a saida.
- `Stream.PrimaryRetrySeconds`: intervalo para testar o retorno do link principal enquanto toca uma reserva.
- `Audio.Backend`: `DirectSound`, `Wasapi` ou `WaveOut`.
- `Audio.OutputDeviceId`: ID da placa de audio escolhida pelo menu.
- `Service.Name`: nome interno do servico Windows.
- `Service.DisplayName`: nome exibido no Windows Services.

## Comandos do EXE

```powershell
.\GTF RX Tlink.exe --console
.\GTF RX Tlink.exe --list-devices
.\GTF RX Tlink.exe --open-config
.\GTF RX Tlink.exe --export-logs
.\GTF RX Tlink.exe --install-service
.\GTF RX Tlink.exe --start-service
.\GTF RX Tlink.exe --stop-service
.\GTF RX Tlink.exe --restart-service
.\GTF RX Tlink.exe --uninstall-service
```

Execute instalacao/remocao do servico em Prompt de Comando ou PowerShell como Administrador.

## Observacao sobre audio em servico

Audio em Windows Service depende da conta usada pelo servico. Se `LocalSystem` nao enxergar a placa correta, configure o servico para rodar com uma conta de usuario local que tenha acesso ao dispositivo de audio.

## Logs

Os logs ficam em:

```text
logs\radio-stream-player.log
```

No console, use a opcao `13 - Exportar logs para TXT` para gerar uma copia `.txt` com tudo que aconteceu ate aquele momento.

Use a opcao `14 - Configurar buffer de audio` para ajustar o tamanho do buffer sem editar o JSON manualmente.
