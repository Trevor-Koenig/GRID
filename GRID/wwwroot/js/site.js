 //Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
 //for details on configuring this project to bundle and minify static web assets.

 //Write your JavaScript code.
const lightModeBtn = document.getElementById('lightModeBtn');
const darkModeBtn = document.getElementById('darkModeBtn');
const htmlElement = document.documentElement; // Targets the <html> element

lightModeBtn.addEventListener('click', () => {
    htmlElement.setAttribute('data-bs-theme', 'light');
    localStorage.setItem('bootstrapTheme', 'light'); // Optional: store preference
});

darkModeBtn.addEventListener('click', () => {
    htmlElement.setAttribute('data-bs-theme', 'dark');
    localStorage.setItem('bootstrapTheme', 'dark'); // Optional: store preference
});

// Optional: Load saved theme preference on page load
document.addEventListener('DOMContentLoaded', () => {
    const savedTheme = localStorage.getItem('bootstrapTheme');
    var showDarkBtn = false;
    if (savedTheme) {
        showDarkBtn = (savedTheme != 'dark');
        htmlElement.setAttribute('data-bs-theme', savedTheme);
    } else {
        // Set a default theme if no preference is saved
        htmlElement.setAttribute('data-bs-theme', 'dark');
    }
});