// Ajustes del navegador para la build WebGL de Smog Buster.
mergeInto(LibraryManager.library, {
    // El clic derecho lanza semillas: sin esto el navegador abre su menú contextual,
    // la página pierde el foco y el juego se pausa.
    SmogBuster_DisableContextMenu: function () {
        document.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    }
});
