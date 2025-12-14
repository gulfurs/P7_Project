document.addEventListener("DOMContentLoaded", () => {
  const root = document.querySelector(".uml-carousel");
  if (!root) return;

  const track = root.querySelector(".uml-track");
  const slides = Array.from(root.querySelectorAll(".uml-slide"));
  const prevBtn = root.querySelector(".prev");
  const nextBtn = root.querySelector(".next");
  const dotsWrap = root.querySelector(".uml-dots");

  let index = 0;

  slides.forEach((_, i) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "uml-dot" + (i === 0 ? " is-active" : "");
    b.setAttribute("aria-label", `Go to slide ${i + 1}`);
    b.addEventListener("click", () => goTo(i));
    dotsWrap.appendChild(b);
  });

  const dots = Array.from(root.querySelectorAll(".uml-dot"));

  function update() {
    track.style.transform = `translateX(-${index * 100}%)`;
    slides.forEach((s, i) => s.classList.toggle("is-active", i === index));
    dots.forEach((d, i) => d.classList.toggle("is-active", i === index));
  }

  function goTo(i) {
    index = (i + slides.length) % slides.length;
    update();
  }

  prevBtn.addEventListener("click", () => goTo(index - 1));
  nextBtn.addEventListener("click", () => goTo(index + 1));

  root.addEventListener("keydown", (e) => {
    if (e.key === "ArrowLeft") goTo(index - 1);
    if (e.key === "ArrowRight") goTo(index + 1);
  });
  root.tabIndex = 0;

  update();
});
