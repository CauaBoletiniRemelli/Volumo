// Em desenvolvimento não há cache: cada recarga busca a versão nova do servidor.
// A versão publicada (com cache para uso offline) é a service-worker.published.js.
self.addEventListener('fetch', () => { });
