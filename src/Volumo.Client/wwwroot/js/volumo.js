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
};
