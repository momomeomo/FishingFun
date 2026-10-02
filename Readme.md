# <b>stopped developing srry prob still works</b>

FishingFun source repository: https://github.com/julianperrott/FishingFun

Modified FishingFun please check the original instead

-----------------------------------------------------------------------
## Fishing in Lava

To get the 'Fire Ammonite Angler' achievement you need to fish in Lava. Lava is red making it impossible to see the red feather, so you need to switch to the blue feather. On the main page click the orange configuration button, then change the 'Watch Feather' combo from Red to 'Blue'

Use a macro like this on your fishing key:

        /use Fire Ammonite Bait
        /cast Fishing

![Fishing in Lava](/post/img/lava.png)

#### Problem 1: Finding the bobber

The bobber is tiny on the screen, we need to make it easier to find. 

![Screen Zoomed Out](/post/img/fishingfun_zoomedout.jpg)


Changing the character view to fully zoomed in means that the bobber is bigger and there is less clutter on the screen. 

To further simplify finding the bobber, it must appear in the middle half of the screen as viewed by the character. Indicated by the red area in the image below.

![Screen Zoomed In](/post/img/FishingFun_ZoomedIn.jpg)

The bobber is pretty easy for us to spot now, but a computer needs a simple way to determine where the bobber is. We could train an AI to find the float, but that seems like an over complicated solution. Perhaps we can use the red colour of the bobber to locate it ?

If we find all the red pixels in middle half of the screen, then find the pixel with most red pixels around it then we should have our bobber location !

We can get a bitmap of the screen as below:
<pre class="prettyprint">
public static Bitmap GetBitmap()
{
    var bmpScreen = new Bitmap(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2);
    var graphics = Graphics.FromImage(bmpScreen);
    graphics.CopyFromScreen(Screen.PrimaryScreen.Bounds.Width / 4, Screen.PrimaryScreen.Bounds.Height / 4, 0, 0, bmpScreen.Size);
    graphics.Dispose();
    return bmpScreen;
}
</pre>

#### Problem 2: Determining when a bite has taken place.

When a bite occurs the bobber moves down a few pixels. If we track the position of the bobber while fishing, we can see an obvious change in the Y position when the bite happens.

### Determining the location of the red feather on the bobber

Due to the different times of day and environments in the game, the red bobber feather changes its shade of red, it also has a range of red shades within it. We need to classify all these colours as being red.

![Fishing Bobbers](/post/img/fishingfun_bobbers.png)

The pixels we are looking for are going have an RGB value with a high Red value compared to the Green and Blue. In the colour cube below we are looking for the colours in the back left.

![Colour Cube](/post/img/finshingfun_cube.png)

This is the algorithm I have created to determine redness:

* Red is greater that Blue and Green by a chosen percentage e.g. 200%.
* Blue and Green are reasonably close together.

<pre class="prettyprint" >
public double ColourMultiplier { get; set; } = 0.5;
public double ColourClosenessMultiplier { get; set; } = 2.0;

public bool IsMatch(byte red, byte green, byte blue)
{
    return isBigger(red, green) && isBigger(red, blue) && areClose(blue, green);
}

private bool isBigger(byte red, byte other)
{
    return (red * ColourMultiplier) > other;
}

private bool areClose(byte color1, byte color2)
{
    var max = Math.Max(color1, color2);
    var min = Math.Min(color1, color2);

    return min * ColourClosenessMultiplier > max - 20;
}
</pre>

In the animation below which shows the Red value changing from 0 to 255 within a 2D square of all possible Blue and Green values, the algorithm matches the red colours within the white boundary. These are all the possible colours which it considers as being in the red feather.

![Red Match Animation](/post/img/fishingfun_red.png)

----

### The User Interface

The WPF user interface I created allows the user to see what the bot sees and how it is doing finding the bobber. This 
helps to determine how well it is working.

#### Main Screen

On the left of the UI there is an screenshot which shows the part of the screen being monitored, the bobber position is indicated by a recticle, the recognised red pixels are shown in pure red colour.

![Screenshot1](/post/img/FishingFun_Screenshot1.jpg)



In the top right the amplitude of the bobber is shown in an animated graph ([lvcharts.net](https://lvcharts.net/)). It moves up and down a few pixels during fishing. When the bite occurs it drops 7 or more pixels.

![Screenshot Looting](https://raw.githubusercontent.com/julianperrott/FishingFun/master/post/img/Screenshot2.png "Fishing Fun - Looting")

#### Colour Configuration Screen

A second configuration screen allows the investigation of different settings for the 'Red' pixel detection.

![Screenshot3](/post/img/FishingFun_Screenshot3.jpg)


### Console Version

A console version is also available if the UI is not needed. It exposes the log so that some feedback on the bot performance is given to the user.

![Screenshot Console](/post/img/FishingFun_Console.png)







