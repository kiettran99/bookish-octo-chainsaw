window.history.scrollRestoration = 'manual';

window.scrollToTopOnLoad = function () {
    window.scrollTo({ top: 0, behavior: 'smooth' });
};

window.onload = function () {
    window.scrollToTopOnLoad();
};
