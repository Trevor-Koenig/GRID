 //Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
 //for details on configuring this project to bundle and minify static web assets.

const root = document.documentElement;

const setTheme = theme => {
    root.dataset.bsTheme = theme;
    localStorage.setItem("theme", theme);
};

// Initialize theme: localStorage → system preference
setTheme(
    localStorage.getItem("theme") ||
    (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light")
);

// Attach listeners for theme buttons
const bind = (selector, theme) =>
    document.querySelectorAll(selector).forEach(btn =>
        btn.addEventListener("click", () => setTheme(theme))
    );

bind(".theme-light-btn", "light");
bind(".theme-dark-btn", "dark");