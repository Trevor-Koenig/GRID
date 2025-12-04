document.addEventListener("DOMContentLoaded", function () {
    document.body.classList.add('index-page');

});

document.addEventListener('click', function (event) {
    // Find the closest ancestor (or itself) with class "div-link"
    const linkDiv = event.target.closest('.div-link');

    // If none found, do nothing
    if (!linkDiv) return;

    // Get data-href (or whatever attribute you use)
    const targetUrl = linkDiv.getAttribute('data-href');
    if (!targetUrl) return;

    // Redirect
    window.location.href = targetUrl;
});