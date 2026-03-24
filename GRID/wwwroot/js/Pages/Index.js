document.addEventListener("DOMContentLoaded", function () {
    document.body.classList.add('index-page');

    // set up listeners for sections
    // Select all headings that have an ID
    const sections = document.querySelectorAll("section[id]");

    const observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const id = entry.target.id;
                history.replaceState(null, null, "#" + id);
            }
        });
    }, {
        rootMargin: "0px 0px -70% 0px",   // Adjust which point marks a section as 'active'
        threshold: 0
    });

    sections.forEach(section => observer.observe(section));

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