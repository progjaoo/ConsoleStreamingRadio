# GTF RX Tlink - Documentacao do Sistema

Este projeto e um player console em C#/.NET para retransmissao de radio via stream HTTP/HTTPS. Ele foi preparado para rodar manualmente em console e tambem como servico Windows usando WinSW.

## O que foi implementado

- Player automatico de stream usando NAudio.
- Arquivo de configuracao externo `radio-config.json`.
- Selecao de placa de audio pelo console.
- Suporte a `DirectSound`, `Wasapi` e `WaveOut`.
- Buffer de audio configuravel.
- Tres links de transmissao:
  - Link principal.
  - Link reserva 1.
  - Link reserva 2.
- Failover automatico entre links.
- Monitoramento do link principal enquanto toca uma reserva.
- Retorno automatico ao link principal quando ele volta a receber dados.
- Logs em arquivo.
- Exportacao de logs para TXT.
- Menu administrativo em console.
- Comandos por argumento.
- Arquivo de configuracao para WinSW.

## Estrutura principal

```text
Program.cs                    Entrada da aplicacao e comandos CLI.
ConsoleMenu.cs                Menu administrativo do console.
StreamPlayerService.cs        Player, buffer, failover e monitoramento de links.
AudioDeviceService.cs         Listagem e abertura das placas de audio.
ConfigService.cs              Leitura, criacao, validacao e watch do JSON.
AppConfig.cs                  Modelos de configuracao.
AppLogger.cs                  Logs e exportacao TXT.
WindowsServiceCommands.cs     Comandos antigos via sc.exe.
WindowsServiceHost.cs         Host antigo de servico via SCM/PInvoke.
radio-config.json             Configuracao padrao.
winsw/                        Arquivos para rodar com WinSW.
```

## Configuracao

O arquivo `radio-config.json` fica ao lado do EXE publicado. Ele e copiado no build/publish.

Campos principais:

- `Stream.Url`: URL principal, mantida por compatibilidade.
- `Stream.Links`: lista com ate 3 links.
- `Stream.Links[0]`: sempre o link principal.
- `Stream.Links[1]`: reserva 1.
- `Stream.Links[2]`: reserva 2.
- `Stream.BufferSeconds`: tamanho total do buffer de audio.
- `Stream.PrebufferSeconds`: quanto carregar antes de iniciar a saida.
- `Stream.BufferUnderrunFailSeconds`: tempo sem audio no buffer para considerar queda.
- `Stream.PrimaryRetrySeconds`: intervalo para testar retorno do principal quando estiver em reserva.
- `Stream.LinkHealthTimeoutSeconds`: timeout do teste de saude do link.
- `Audio.Backend`: `DirectSound`, `Wasapi` ou `WaveOut`.
- `Audio.OutputDeviceId`: ID salvo da placa escolhida.
- `Audio.OutputDeviceName`: nome salvo da placa escolhida.
- `Service.Name`: nome interno do servico.
- `Service.DisplayName`: nome exibido no Windows Services.

Exemplo de links:

```json
"Links": [
  {
    "Name": "Principal",
    "Url": "https://stm19.srvstm.com:7080/stream",
    "Enabled": true
  },
  {
    "Name": "Reserva 1",
    "Url": "https://exemplo.com/reserva1",
    "Enabled": true
  },
  {
    "Name": "Reserva 2",
    "Url": "https://exemplo.com/reserva2",
    "Enabled": true
  }
]
```

## Como o failover funciona

1. O player sempre tenta iniciar pelo link principal.
2. O audio recebido entra em um buffer local.
3. O player espera o `PrebufferSeconds` antes de tocar.
4. Se o link atual cair, o player continua tocando o que ainda esta no buffer.
5. Quando o buffer fica sem audio pelo tempo configurado em `BufferUnderrunFailSeconds`, o player troca para o proximo link ativo.
6. Se estiver tocando uma reserva, o sistema testa o link principal a cada `PrimaryRetrySeconds`.
7. Quando o principal volta a receber bytes de audio, o player reinicia e retorna para ele.

Observacao: o teste do link principal considera que o link voltou quando consegue receber bytes HTTP. Se o servidor transmitir silencio, ainda sera considerado ativo.

## Menu do console

Execute:

```powershell
.\GTF RX Tlink.exe --console
```

Opcoes:

```text
1  - Mostrar status do player local
2  - Listar placas de audio
3  - Selecionar placa de audio
4  - Alterar URL do stream principal
5  - Abrir arquivo de configuracao
6  - Reiniciar player local
7  - Instalar servico Windows antigo via sc.exe
8  - Iniciar servico Windows antigo via sc.exe
9  - Parar servico Windows antigo via sc.exe
10 - Reiniciar servico Windows antigo via sc.exe
11 - Remover servico Windows antigo via sc.exe
12 - Configurar links de transmissao
13 - Exportar logs para TXT
14 - Configurar buffer de audio
0  - Sair
```

Para operacao atual como servico, prefira WinSW.

## Comandos do EXE

```powershell
.\GTF RX Tlink.exe --console
.\GTF RX Tlink.exe --run
.\GTF RX Tlink.exe --list-devices
.\GTF RX Tlink.exe --open-config
.\GTF RX Tlink.exe --export-logs
.\GTF RX Tlink.exe --help
```

Comandos antigos de servico via `sc.exe`:

```powershell
.\GTF RX Tlink.exe --install-service
.\GTF RX Tlink.exe --start-service
.\GTF RX Tlink.exe --stop-service
.\GTF RX Tlink.exe --restart-service
.\GTF RX Tlink.exe --uninstall-service
.\GTF RX Tlink.exe --status-service
```

Esses comandos antigos continuam no codigo, mas a recomendacao atual e usar WinSW.

## Publicar no Windows

No PC Windows com .NET SDK instalado:

```powershell
dotnet restore
dotnet build "RadioStreamPlayer.sln"
dotnet publish "GTF RX Tlink.csproj" -c Release -o ".\publish"
```

Depois entre na pasta:

```powershell
cd .\publish
```

Teste primeiro em console:

```powershell
.\GTF RX Tlink.exe --console
```

Antes de instalar como servico, confirme:

- Stream toca em console.
- Placa de audio correta foi selecionada.
- `radio-config.json` esta correto.
- Logs estao sendo gerados.

## Rodar como servico com WinSW

Consulte tambem [winsw/README-WINSW.md](winsw/README-WINSW.md).

Na pasta publicada, deixe:

```text
GTF RX Tlink.exe
GTF RX Tlink.dll
radio-config.json
GTF-RX-Tlink-Service.exe
GTF-RX-Tlink-Service.xml
NAudio*.dll
```

`GTF-RX-Tlink-Service.exe` e o WinSW renomeado.

`GTF-RX-Tlink-Service.xml` deve ser copiado de:

```text
winsw\GTF-RX-Tlink-Service.xml
```

Instale em PowerShell como Administrador:

```powershell
.\GTF-RX-Tlink-Service.exe install
.\GTF-RX-Tlink-Service.exe start
.\GTF-RX-Tlink-Service.exe status
```

Parar/remover:

```powershell
.\GTF-RX-Tlink-Service.exe stop
.\GTF-RX-Tlink-Service.exe uninstall
```

O XML chama:

```xml
<executable>%BASE%\GTF RX Tlink.exe</executable>
<arguments>--run</arguments>
```

Ou seja: o WinSW vira o servico e executa o player sem menu interativo.

## Logs

Logs do aplicativo:

```text
logs\radio-stream-player.log
```

Exportar logs:

```powershell
.\GTF RX Tlink.exe --export-logs
```

Ou pelo menu:

```text
13 - Exportar logs para TXT
```

O WinSW tambem gera logs proprios na pasta do servico.

## Audio em servico Windows

Audio em Windows Service depende da conta usada pelo servico. Se o servico iniciar mas nao sair audio:

- Teste primeiro com `--console`.
- Se funcionar no console, mas nao no servico, o problema provavelmente e permissao/sessao.
- Configure o servico para rodar com um usuario local que tenha acesso a placa de audio.
- Evite depender de `LocalSystem` para audio.
- Em alguns ambientes, uma tarefa agendada "ao fazer logon" pode ser mais confiavel que servico para audio.

## Checklist para o outro PC

1. Fazer pull do repositorio.
2. Conferir se `radio-config.json` tem os links corretos.
3. Rodar `dotnet restore`.
4. Rodar `dotnet build "RadioStreamPlayer.sln"`.
5. Publicar com `dotnet publish`.
6. Testar `.\GTF RX Tlink.exe --console`.
7. Listar placas no menu.
8. Selecionar a placa correta.
9. Configurar reservas no menu opcao 12.
10. Ajustar buffer no menu opcao 14.
11. Exportar logs no menu opcao 13 se precisar diagnosticar.
12. Copiar WinSW renomeado e XML para a pasta publicada.
13. Instalar e iniciar o servico com PowerShell Administrador.

## Validacoes feitas neste ambiente

No Mac, foi possivel validar compilacao e publish:

```text
dotnet build RadioStreamPlayer.sln
dotnet publish "GTF RX Tlink.csproj" -c Release
```

Ambos passaram sem erros.

Audio, WASAPI, DirectSound e servico Windows precisam ser testados no Windows.
