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
