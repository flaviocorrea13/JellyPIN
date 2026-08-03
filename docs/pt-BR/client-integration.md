# Integração com clientes nativos

O servidor JellyPIN já bloqueia requisições do Android TV, Roku, Kodi e outros clientes. Uma alteração no aplicativo nativo melhora a experiência ao reconhecer o bloqueio e mostrar a tela de PIN.

## Fluxo necessário

1. Manter um identificador Jellyfin estável para o dispositivo e enviá-lo em todas as requisições.
2. Antes de abrir a biblioteca protegida, ou após um `403` JellyPIN, consultar `GET /JellyPIN/Status`.
3. Mostrar um teclado numérico para PIN de 4 a 8 dígitos.
4. Enviar `POST /JellyPIN/Unlock` com `{ "pin": "1234" }`, usando o mesmo usuário, token e dispositivo.
5. Repetir uma única vez a navegação ou reprodução original após o desbloqueio.
6. Enviar `POST /JellyPIN/Lock` ao escolher “Bloquear agora”. Logout e encerramento da sessão também são revogados pelo servidor.

Resposta de bloqueio:

```json
{
  "error": "JellyPINLocked",
  "message": "This content is protected by JellyPIN."
}
```

O PIN deve existir somente na memória da tela, ser apagado imediatamente e nunca entrar em logs, métricas ou relatórios de falha.

## Situação atual

| Cliente | Bloqueio no servidor | Tela nativa de PIN |
|---|---:|---:|
| Jellyfin Web / navegador | Sim | Incluída no patch JellyPIN Web |
| Jellyfin Media Player com Web modificado | Sim | Incluída |
| Android TV oficial | Sim | Exige contribuição no cliente |
| Roku oficial | Sim | Exige contribuição no cliente |

Os patches nativos devem ser mantidos nos respectivos repositórios oficiais. Distribuir aplicativos oficiais modificados dentro do plugin prejudicaria assinatura, lojas, atualizações e revisão de segurança.
