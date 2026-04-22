const clamp = (value, min = 0, max = 1) => Math.min(max, Math.max(min, value));
const lerp = (start, end, amount) => start + (end - start) * amount;
const easeOutCubic = (value) => 1 - Math.pow(1 - value, 3);
const easeInOutCubic = (value) => (
    value < 0.5
        ? 4 * value * value * value
        : 1 - Math.pow(-2 * value + 2, 3) / 2
);

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

const storyScenes = [...document.querySelectorAll("[data-story-scene]")].map((section) => ({
    section,
    media: section.querySelector(".story-media"),
    copy: section.querySelector(".story-copy"),
    copyShiftStart: section.classList.contains("story-section--reverse") ? 72 : -72
}));

let sceneMeasurements = [];
let storyTicking = false;

const canAnimateScenes = () => !prefersReducedMotion && window.innerWidth > 1080;

const resetStoryScene = (scene) => {
    if (!scene.media || !scene.copy) {
        return;
    }

    scene.media.style.transform = "none";
    scene.media.style.borderRadius = "";
    scene.media.style.boxShadow = "";
    scene.copy.style.setProperty("--story-copy-opacity", "1");
    scene.copy.style.setProperty("--story-copy-shift-x", "0px");
    scene.copy.style.setProperty("--story-copy-shift-y", "0px");
};

const measureStoryScenes = () => {
    sceneMeasurements = [];

    if (!canAnimateScenes()) {
        storyScenes.forEach(resetStoryScene);
        return;
    }

    for (const scene of storyScenes) {
        if (!scene.media || !scene.copy) {
            continue;
        }

        resetStoryScene(scene);

        const finalRect = scene.media.getBoundingClientRect();
        if (!finalRect.width || !finalRect.height) {
            continue;
        }

        const edgeInset = Math.max(24, window.innerWidth * 0.035);
        const mediaAspect = finalRect.width / finalRect.height;
        const maxWidthByHeight = (window.innerHeight - edgeInset * 2) * mediaAspect;
        const startWidth = Math.min(window.innerWidth - edgeInset * 2, maxWidthByHeight, 1440);
        const startHeight = startWidth / mediaAspect;
        const startCenterX = window.innerWidth / 2;
        const startCenterY = window.innerHeight / 2 + Math.min(18, window.innerHeight * 0.02);
        const finalCenterX = finalRect.left + finalRect.width / 2;
        const finalCenterY = finalRect.top + finalRect.height / 2;

        sceneMeasurements.push({
            ...scene,
            startTranslateX: startCenterX - finalCenterX,
            startTranslateY: startCenterY - finalCenterY,
            startScale: startWidth / finalRect.width
        });
    }

    updateStoryScenes();
};

const updateStoryScenes = () => {
    if (!canAnimateScenes()) {
        storyScenes.forEach(resetStoryScene);
        return;
    }

    for (const scene of sceneMeasurements) {
        const rect = scene.section.getBoundingClientRect();
        const rawProgress = clamp(-rect.top / Math.max(1, rect.height - window.innerHeight), 0, 1);
        const mediaProgress = easeInOutCubic(clamp((rawProgress - 0.03) / 0.67, 0, 1));
        const copyProgress = easeOutCubic(clamp((rawProgress - 0.18) / 0.36, 0, 1));
        const translateX = lerp(scene.startTranslateX, 0, mediaProgress);
        const translateY = lerp(scene.startTranslateY, 0, mediaProgress);
        const scale = lerp(scene.startScale, 1, mediaProgress);
        const copyShiftX = lerp(scene.copyShiftStart, 0, copyProgress);
        const copyShiftY = lerp(28, 0, copyProgress);
        const shadowY = lerp(64, 36, mediaProgress);
        const shadowBlur = lerp(160, 100, mediaProgress);
        const shadowAlpha = lerp(0.22, 0.12, mediaProgress);

        scene.media.style.transform = `translate3d(${translateX.toFixed(2)}px, ${translateY.toFixed(2)}px, 0) scale(${scale.toFixed(4)})`;
        scene.media.style.borderRadius = `${lerp(30, 18, mediaProgress).toFixed(2)}px`;
        scene.media.style.boxShadow = `0 ${shadowY.toFixed(2)}px ${shadowBlur.toFixed(2)}px rgba(18, 26, 46, ${shadowAlpha.toFixed(3)})`;
        scene.copy.style.setProperty("--story-copy-opacity", copyProgress.toFixed(3));
        scene.copy.style.setProperty("--story-copy-shift-x", `${copyShiftX.toFixed(2)}px`);
        scene.copy.style.setProperty("--story-copy-shift-y", `${copyShiftY.toFixed(2)}px`);
    }
};

const queueStoryUpdate = () => {
    if (storyTicking) {
        return;
    }

    storyTicking = true;
    window.requestAnimationFrame(() => {
        updateStoryScenes();
        storyTicking = false;
    });
};

window.addEventListener("resize", measureStoryScenes, { passive: true });
window.addEventListener("scroll", queueStoryUpdate, { passive: true });
window.addEventListener("load", measureStoryScenes, { once: true });

if (document.fonts?.ready) {
    document.fonts.ready.then(() => {
        measureStoryScenes();
    });
}

measureStoryScenes();

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
