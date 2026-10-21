// Pontes pequenas entre o Blazor e o navegador. Nada aqui guarda token ou dado pessoal.
window.volumo = {
    turnstile: {
        _carregando: null,

        // Carrega o script da Cloudflare só quando um formulário precisa dele.
        carregar() {
            if (window.turnstile) {
                return Promise.resolve();
            }

            if (!this._carregando) {
                this._carregando = new Promise((resolve, reject) => {
                    const script = document.createElement('script');
                    script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
                    script.async = true;
                    script.onload = () => resolve();
                    script.onerror = () => { this._carregando = null; reject(new Error('turnstile')); };
                    document.head.appendChild(script);
                });
            }

            return this._carregando;
        },

        async renderizar(elemento, siteKey, dotnet) {
            await this.carregar();
            return window.turnstile.render(elemento, {
                sitekey: siteKey,
                theme: 'dark',
                language: 'pt-br',
                callback: token => dotnet.invokeMethodAsync('Resolvido', token),
                'expired-callback': () => dotnet.invokeMethodAsync('Expirado'),
                'error-callback': () => dotnet.invokeMethodAsync('Expirado'),
            });
        },

        reiniciar(widgetId) {
            if (window.turnstile && widgetId) {
                window.turnstile.reset(widgetId);
            }
        },

        remover(widgetId) {
            if (window.turnstile && widgetId) {
                window.turnstile.remove(widgetId);
            }
        },
    },

    copiar(texto) {
        return navigator.clipboard.writeText(texto);
    },

    // Dados de treino guardados no aparelho (IndexedDB) para usar sem internet. Só JSON de
    // treinos e sessões, nunca token. Chaves prefixadas pelo id do usuário.
    local: {
        _banco: null,

        _abrir() {
            if (!this._banco) {
                this._banco = new Promise((resolve, reject) => {
                    const pedido = indexedDB.open('volumo', 1);
                    pedido.onupgradeneeded = () => pedido.result.createObjectStore('dados');
                    pedido.onsuccess = () => resolve(pedido.result);
                    pedido.onerror = () => { this._banco = null; reject(pedido.error); };
                });
            }

            return this._banco;
        },

        async _executar(modo, acao) {
            const banco = await this._abrir();
            return new Promise((resolve, reject) => {
                const transacao = banco.transaction('dados', modo);
                const pedido = acao(transacao.objectStore('dados'));
                transacao.oncomplete = () => resolve(pedido?.result ?? null);
                transacao.onerror = () => reject(transacao.error);
            });
        },

        ler(chave) {
            return this._executar('readonly', store => store.get(chave));
        },

        gravar(chave, valor) {
            return this._executar('readwrite', store => { store.put(valor, chave); });
        },

        apagar(chave) {
            return this._executar('readwrite', store => { store.delete(chave); });
        },

        apagarPrefixo(prefixo) {
            return this._executar('readwrite', store => { store.delete(IDBKeyRange.bound(prefixo, prefixo + '￿')); });
        },
    },

    conexao: {
        online() {
            return navigator.onLine;
        },

        // Avisa o Blazor quando a internet cai ou volta (para enviar a fila na hora).
        observar(dotnet) {
            window.addEventListener('online', () => dotnet.invokeMethodAsync('MudouConexao', true));
            window.addEventListener('offline', () => dotnet.invokeMethodAsync('MudouConexao', false));
        },
    },

    // Mantém a tela acesa durante o treino (quando o navegador permite).
    telaAcesa: {
        _trava: null,
        _ligada: false,
        _ouvindo: false,

        async ligar() {
            this._ligada = true;
            await this._pedir();
            if (!this._ouvindo) {
                this._ouvindo = true;
                document.addEventListener('visibilitychange', () => {
                    if (this._ligada && document.visibilityState === 'visible') {
                        this._pedir();
                    }
                });
            }
        },

        async _pedir() {
            try {
                if ('wakeLock' in navigator && !this._trava) {
                    this._trava = await navigator.wakeLock.request('screen');
                    this._trava.addEventListener('release', () => { this._trava = null; });
                }
            } catch {
                // Sem suporte ou bateria fraca: segue sem.
            }
        },

        desligar() {
            this._ligada = false;
            this._trava?.release();
            this._trava = null;
        },
    },

    vibrar(padrao) {
        if ('vibrate' in navigator) {
            navigator.vibrate(padrao);
        }
    },
};
