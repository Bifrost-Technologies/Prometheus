// pulse.ts
const canvas = document.getElementById('forestCanvas') as HTMLCanvasElement;
const ctx = canvas.getContext('2d')!;
const img = new Image();
img.src = '../assets/img/mystical-forest-8bit.png';  // your generated image file

// Staff position (tweak these to match your artwork!)
const staffX = 300;
const staffY = 350;

// Animation params
const baseRadius = 20;
const pulseRange = 8;
const pulseSpeed = 0.004; // lower = slower

img.onload = () => requestAnimationFrame(animate);

function animate(time: number) {
    const t = time * pulseSpeed;
    // Compute a smooth oscillation between -1 and 1
    const oscillation = Math.sin(t);
    // Map to [baseRadius, baseRadius + pulseRange]
    const glowRadius = baseRadius + (oscillation + 1) / 2 * pulseRange;

    // Draw base image
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

    // Create radial gradient for glow
    const grad = ctx.createRadialGradient(
        staffX, staffY, glowRadius * 0.4,
        staffX, staffY, glowRadius
    );
    grad.addColorStop(0, 'rgba(180, 255, 200, 0.6)');
    grad.addColorStop(1, 'rgba(180, 255, 200, 0)');

    // Draw glow circle
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(staffX, staffY, glowRadius, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    requestAnimationFrame(animate);
}