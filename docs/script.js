const revealObserver = new IntersectionObserver((entries) => {
    for (const entry of entries) {
        if (entry.isIntersecting) {
            entry.target.classList.add("is-visible");
        }
    }
}, {
    threshold: 0.16,
    rootMargin: "0px 0px -10% 0px"
});

document.querySelectorAll(".reveal").forEach((element) => {
    revealObserver.observe(element);
});

const root = document.documentElement;
const themeButtons = document.querySelectorAll("[data-theme-choice]");
const themeColorMeta = document.querySelector('meta[name="theme-color"]');
const header = document.querySelector(".site-header");
const themeColors = {
    light: "#f5f2ea",
    dark: "#0b0d12"
};

const applyTheme = (theme) => {
    root.dataset.theme = theme;
    localStorage.setItem("winnotch-site-theme", theme);
    themeButtons.forEach((button) => {
        button.classList.toggle("is-active", button.dataset.themeChoice === theme);
    });

    if (themeColorMeta) {
        themeColorMeta.setAttribute("content", themeColors[theme] || themeColors.light);
    }
};

const currentTheme = root.dataset.theme || "light";
applyTheme(currentTheme);

themeButtons.forEach((button) => {
    button.addEventListener("click", () => {
        applyTheme(button.dataset.themeChoice || "light");
    });
});

const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const videoObserver = new IntersectionObserver((entries) => {
    for (const entry of entries) {
        const video = entry.target;
        const start = Number(video.dataset.start || "0");

        if (entry.isIntersecting) {
            if (video.dataset.ready === "true" && video.currentTime < start) {
                video.currentTime = start;
            }

            if (!prefersReducedMotion) {
                video.play().catch(() => {});
            }
        } else {
            video.pause();
        }
    }
}, {
    threshold: 0.45
});

document.querySelectorAll(".demo-video").forEach((video) => {
    const start = Number(video.dataset.start || "0");

    const seekToStart = () => {
        try {
            if (video.duration && start < video.duration) {
                video.currentTime = start;
            }
            video.dataset.ready = "true";

            if (!prefersReducedMotion) {
                video.play().catch(() => {});
            }
        } catch {
            video.dataset.ready = "true";
        }
    };

    video.addEventListener("loadedmetadata", seekToStart, { once: true });
    video.addEventListener("ended", () => {
        video.currentTime = start;
        if (!prefersReducedMotion) {
            video.play().catch(() => {});
        }
    });

    videoObserver.observe(video);
});

if (header) {
    let lastScrollY = window.scrollY;
    let isTicking = false;

    const updateHeader = () => {
        const currentScrollY = window.scrollY;
        const scrollingDown = currentScrollY > lastScrollY;

        header.classList.toggle("site-header--scrolled", currentScrollY > 16);

        if (!prefersReducedMotion && currentScrollY > 120 && scrollingDown) {
            header.classList.add("site-header--hidden");
        } else {
            header.classList.remove("site-header--hidden");
        }

        lastScrollY = currentScrollY;
        isTicking = false;
    };

    window.addEventListener("scroll", () => {
        if (!isTicking) {
            window.requestAnimationFrame(updateHeader);
            isTicking = true;
        }
    }, { passive: true });

    updateHeader();
}
