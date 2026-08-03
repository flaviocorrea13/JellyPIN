# Gerenciador do JellyPIN Web

O gerenciador instala ou atualiza o pacote JellyPIN Web na instalação Linux/LXC do Jellyfin. Antes de alterar qualquer arquivo, ele:

1. confirma a versão instalada do Jellyfin Web;
2. baixa o ZIP e o checksum SHA-256 da release oficial;
3. rejeita caminhos inseguros dentro do ZIP;
4. preserva o `config.json` atual;
5. cria um backup completo com data e metadados;
6. troca o diretório Web e reinicia o Jellyfin;
7. restaura automaticamente a versão anterior se o serviço não iniciar.

## Preparação

Baixe o script e examine seu conteúdo antes de executá-lo como administrador:

```bash
curl -fL \
  https://raw.githubusercontent.com/flaviocorrea13/JellyPIN/main/scripts/jellypin-web-manager.sh \
  -o /tmp/jellypin-web-manager.sh

less /tmp/jellypin-web-manager.sh
chmod +x /tmp/jellypin-web-manager.sh
```

## Instalar ou atualizar

Instalar a versão mais recente:

```bash
sudo /tmp/jellypin-web-manager.sh install
```

Instalar uma versão específica:

```bash
sudo /tmp/jellypin-web-manager.sh install 0.8.0.0
```

O comando é interrompido se a versão instalada do Jellyfin Web não for `10.11.11`. Não use `--force-version` sem verificar manualmente que o pacote é compatível.

## Consultar estado e backups

```bash
/tmp/jellypin-web-manager.sh status
/tmp/jellypin-web-manager.sh backups
```

Os backups ficam em `/var/lib/jellypin/web-backups`. Cada backup contém um `web.tar.gz` e um arquivo `metadata`. O link `latest` aponta para o backup mais recente.

## Restaurar

Restaurar o backup mais recente:

```bash
sudo /tmp/jellypin-web-manager.sh restore
```

Restaurar um backup escolhido na listagem:

```bash
sudo /tmp/jellypin-web-manager.sh restore 20260803T120000Z-pre-install
```

Antes da restauração, o estado atual também é salvo como um novo backup `pre-restore`. Assim é possível desfazer uma restauração.

## Caminhos personalizados

As instalações padrão não precisam destas opções. Para instalações diferentes:

```bash
sudo env \
  JELLYPIN_WEB_DIR=/caminho/do/web \
  JELLYPIN_BACKUP_DIR=/caminho/dos/backups \
  JELLYPIN_SERVICE=jellyfin \
  /tmp/jellypin-web-manager.sh install 0.8.0.0
```

Nunca defina o diretório de backup dentro do diretório Jellyfin Web.

