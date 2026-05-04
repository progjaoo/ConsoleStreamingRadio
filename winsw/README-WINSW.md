# Rodar o GTF RX Tlink como servico com WinSW

O WinSW e um wrapper: ele vira o servico Windows e executa o nosso console por baixo. Por isso, o argumento correto para o nosso app e `--run`, nao `--service`.

## Arquivos na pasta publicada

Depois de publicar o projeto, deixe os arquivos assim na mesma pasta:

```text
GTF RX Tlink.exe
GTF RX Tlink.dll
radio-config.json
GTF-RX-Tlink-Service.exe
GTF-RX-Tlink-Service.xml
NAudio*.dll
```

`GTF-RX-Tlink-Service.exe` deve ser o executavel do WinSW renomeado.

`GTF-RX-Tlink-Service.xml` deve ser uma copia do arquivo `winsw/GTF-RX-Tlink-Service.xml` deste repositorio.

## Instalar

Abra PowerShell como Administrador dentro da pasta publicada:

```powershell
.\GTF-RX-Tlink-Service.exe install
.\GTF-RX-Tlink-Service.exe start
.\GTF-RX-Tlink-Service.exe status
```

## Parar ou remover

```powershell
.\GTF-RX-Tlink-Service.exe stop
.\GTF-RX-Tlink-Service.exe uninstall
```

## Logs

O WinSW gera logs proprios na pasta do servico. O aplicativo tambem gera:

```text
logs\radio-stream-player.log
```

Para exportar os logs do aplicativo:

```powershell
.\GTF RX Tlink.exe --export-logs
```

## Audio em servico

Se o servico iniciar mas nao sair audio, configure o servico para rodar com um usuario local que tenha acesso a placa de audio. Audio em servico Windows pode ser limitado pela sessao isolada do Windows.
