 //Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
 //for details on configuring this project to bundle and minify static web assets.

const root = document.documentElement;

const saveThemeToAccount = async (theme) => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (!token) return; // not authenticated or no antiforgery token on page
    try {
        await fetch(`/api/theme?theme=${theme}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': token }
        });
    } catch { /* ignore network errors */ }
};

const setTheme = (theme, persist = true) => {
    root.dataset.bsTheme = theme;
    localStorage.setItem("theme", theme);
    if (persist) saveThemeToAccount(theme);
};

// Initialize: user account preference → localStorage → system preference
const userThemeMeta = document.querySelector('meta[name="user-theme"]');
const userTheme = userThemeMeta?.content;

setTheme(
    userTheme ||
    localStorage.getItem("theme") ||
    (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"),
    false // don't re-save on init
);

// Attach listeners for theme buttons
const bind = (selector, theme) =>
    document.querySelectorAll(selector).forEach(btn =>
        btn.addEventListener("click", () => setTheme(theme))
    );

bind(".theme-light-btn", "light");
bind(".theme-dark-btn", "dark");
