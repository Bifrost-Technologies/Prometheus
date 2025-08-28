// pulse.ts
var canvas = document.getElementById('forestCanvas');
var ctx = canvas.getContext('2d');
var img = new Image();
img.src = '../assets/img/mystical-forest-8bit.png'; // your generated image file
// Staff position (tweak these to match your artwork!)
var staffX = 300;
var staffY = 350;
// Animation params
var baseRadius = 20;
var pulseRange = 8;
var pulseSpeed = 0.004; // lower = slower
img.onload = function () { return requestAnimationFrame(animate); };
function animate(time) {
    var t = time * pulseSpeed;
    // Compute a smooth oscillation between -1 and 1
    var oscillation = Math.sin(t);
    // Map to [baseRadius, baseRadius + pulseRange]
    var glowRadius = baseRadius + (oscillation + 1) / 2 * pulseRange;
    // Draw base image
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    // Create radial gradient for glow
    var grad = ctx.createRadialGradient(staffX, staffY, glowRadius * 0.4, staffX, staffY, glowRadius);
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
