import dialogHelper from '../dialogHelper/dialogHelper';
import alert from '../alert';
import { ServerConnections } from '../../lib/jellyfin-apiclient';
import '../../elements/emby-button/emby-button';
import '../../elements/emby-input/emby-input';
import '../formdialog.scss';

function value(response, camelCase, pascalCase) {
    return response?.[camelCase] ?? response?.[pascalCase];
}

async function getAccess(itemId, serverId) {
    const apiClient = ServerConnections.getApiClient(serverId);
    try {
        const response = await apiClient.ajax({
            type: 'GET',
            url: apiClient.getUrl(`JellyPIN/Items/${encodeURIComponent(itemId)}/Access`, { _: Date.now() }),
            dataType: 'json'
        });
        return { apiClient, response };
    } catch (error) {
        if (error?.status === 404) return null;
        throw error;
    }
}

function requestPin(apiClient) {
    return new Promise(resolve => {
        const dlg = dialogHelper.createDialog({ removeOnClose: true, scrollY: false, size: 'small' });
        dlg.classList.add('formDialog');
        dlg.innerHTML = `
            <div class="formDialogContent smoothScrollY">
                <div class="dialogContentInner dialog-content-centered">
                    <h2>Conteúdo protegido</h2>
                    <p>Digite o PIN do JellyPIN para continuar.</p>
                    <form class="jellyPinUnlockForm">
                        <div class="inputContainer">
                            <label class="inputLabel" for="JellyPinPlaybackPin">PIN</label>
                            <input id="JellyPinPlaybackPin" type="password" inputmode="numeric" pattern="[0-9]{4,8}" minlength="4" maxlength="8" required is="emby-input" autocomplete="off">
                        </div>
                        <p class="jellyPinError" style="color:var(--jf-palette-error-main);display:none"></p>
                        <button is="emby-button" type="submit" class="raised button-submit block"><span>Desbloquear</span></button>
                        <button is="emby-button" type="button" class="raised block btnCancel"><span>Cancelar</span></button>
                    </form>
                </div>
            </div>`;

        let accepted = false;
        const form = dlg.querySelector('.jellyPinUnlockForm');
        const pinInput = dlg.querySelector('#JellyPinPlaybackPin');
        const errorText = dlg.querySelector('.jellyPinError');
        dlg.addEventListener('close', () => resolve(accepted));
        dlg.querySelector('.btnCancel').addEventListener('click', () => dialogHelper.close(dlg));
        form.addEventListener('submit', async event => {
            event.preventDefault();
            errorText.style.display = 'none';
            try {
                await apiClient.ajax({
                    type: 'POST',
                    url: apiClient.getUrl('JellyPIN/Unlock'),
                    data: JSON.stringify({ pin: pinInput.value }),
                    contentType: 'application/json',
                    dataType: 'json'
                });
                accepted = true;
                pinInput.value = '';
                dialogHelper.close(dlg);
            } catch (error) {
                errorText.textContent = error?.status === 429
                    ? 'Muitas tentativas. Aguarde antes de tentar novamente.'
                    : 'PIN incorreto.';
                errorText.style.display = 'block';
                pinInput.value = '';
                pinInput.focus();
            }
        });
        dialogHelper.open(dlg).then(() => pinInput.focus());
    });
}

export async function ensureJellyPinPlaybackAccess(items) {
    try {
        for (const item of items) {
            const access = await getAccess(item.Id, item.ServerId);
            if (!access) continue;
            const isProtected = value(access.response, 'protected', 'Protected') ?? false;
            const allowed = value(access.response, 'allowed', 'Allowed') ?? false;
            if (isProtected && !allowed) return requestPin(access.apiClient);
        }
        return true;
    } catch {
        await alert('Não foi possível consultar o JellyPIN. A reprodução foi cancelada.', 'JellyPIN');
        return false;
    }
}

export async function ensureJellyPinLibraryAccess(itemId, serverId) {
    try {
        const access = await getAccess(itemId, serverId);
        if (!access) return true;
        const isProtected = value(access.response, 'protected', 'Protected') ?? false;
        const allowed = value(access.response, 'allowed', 'Allowed') ?? false;
        if (!isProtected || allowed) return true;
        return requestPin(access.apiClient);
    } catch {
        await alert('Não foi possível consultar o JellyPIN. A biblioteca permaneceu bloqueada.', 'JellyPIN');
        return false;
    }
}
