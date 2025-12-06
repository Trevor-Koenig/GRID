document.addEventListener("DOMContentLoaded", function () {
    document.body.classList.add('index-page');

});

document.addEventListener('click', function (event) {
    const divLink = event.target.closest(".div-link");
    if (!divLink) return;

    const href = divLink.dataset.href;
    if (!href) return;

    // Create a temporary anchor element
    const a = document.createElement("a");
    a.href = href;

    // Copy modifier keys so the browser knows how to open the link
    const clickEvent = new MouseEvent("click", {
        view: window,
        bubbles: true,
        cancelable: true,
        ctrlKey: event.ctrlKey,
        metaKey: event.metaKey,
        button: event.button
    });

    a.dispatchEvent(clickEvent);
});