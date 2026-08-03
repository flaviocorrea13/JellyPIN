# JellyPIN

![Ícone do JellyPIN](../../assets/jellypin.png)

Português · [English](../../README.md)

JellyPIN é um controle parental para Jellyfin que protege uma biblioteca completa com PIN. Os desbloqueios temporários ficam vinculados ao usuário e ao dispositivo autenticados, os resultados protegidos são ocultados e as requisições de mídia são bloqueadas no servidor.

Desenvolvido por Flavio Correa ([@flaviocorrea13](https://www.instagram.com/flaviocorrea13)).

## Recursos

- Protege uma biblioteca inteira, sem adicionar etiquetas filme por filme.
- Exige PIN de todos os usuários, inclusive administradores.
- Vincula o desbloqueio ao usuário e ao identificador do dispositivo Jellyfin.
- Renova o prazo enquanto um conteúdo protegido estiver sendo acessado ou reproduzido.
- Revoga o desbloqueio ao sair da conta ou encerrar a sessão Jellyfin.
- Bloqueia todos os dispositivos imediatamente e interrompe reproduções protegidas.
- Lista dispositivos desbloqueados, última atividade e horário de expiração.
- Persiste os 1.000 eventos de auditoria mais recentes sem registrar PIN, hash ou token.
- Oculta itens protegidos de listas, novidades, pesquisas, recomendações e próximos episódios.
- Bloqueia metadados, imagens, legendas, downloads, reprodução direta, HLS e transcodificação quando a requisição identifica um item protegido.

## Instalação

Atualmente são suportados Jellyfin Server 10.11.11 e runtime .NET 9.

1. Abra **Painel → Plugins → Repositórios**.
2. Adicione o repositório `JellyPIN Repository` com a URL:

   ```text
   https://raw.githubusercontent.com/flaviocorrea13/JellyPIN/main/manifest.json
   ```

3. Abra o catálogo, instale JellyPIN e reinicie o servidor.
4. Abra **Painel → Plugins → JellyPIN → Configurações**.
5. Cadastre um PIN de 4 a 8 dígitos e escolha a biblioteca protegida.
6. Instale o pacote JellyPIN Web correspondente para obter o diálogo de PIN no navegador.

Uma atualização do Jellyfin Web pode substituir os arquivos personalizados. Reinstale o JellyPIN Web compatível após atualizar o Jellyfin e nunca instale um pacote criado para outra versão.

## Android TV e Roku

O bloqueio no servidor funciona para Android TV e Roku: metadados e reproduções protegidos são recusados mesmo quando o aplicativo não possui tela JellyPIN. O navegador/Jellyfin Web oferece a solicitação interativa do PIN. Os clientes oficiais Android TV e Roku ainda precisam de alterações próprias para exibir um diálogo nativo, pois não expõem uma extensão visual de plugins. Consulte [Integração com clientes nativos](client-integration.md).

## Segurança

- O PIN é salvo somente como hash PBKDF2-SHA256 com salt aleatório.
- PIN em texto, hash e token Jellyfin nunca entram na auditoria.
- Sessões desbloqueadas e tentativas ficam somente na memória; reiniciar o Jellyfin bloqueia tudo.
- A auditoria fica em `Jellyfin.Plugin.JellyPIN.audit.json`, na pasta de configurações de plugins.
- Use HTTPS fora de uma rede local confiável.
- JellyPIN ainda não passou por auditoria de segurança independente.

## Compilação

```powershell
dotnet restore JellyPIN.slnx
dotnet test JellyPIN.slnx -c Release
dotnet publish Jellyfin.Plugin.JellyPIN/Jellyfin.Plugin.JellyPIN.csproj -c Release
```

O projeto é experimental e usa middleware para cobrir as rotas conhecidas do Jellyfin. Novas rotas do servidor precisam ser revisadas e testadas.
