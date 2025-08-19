
# (1) Read the .png image

library(png)

result <- readPNG('TDIimage_manual.png')

# (2) RGB color of each pixel in the image

resultRGB <- result[,,1:3]   # R, G and B between 0 and 1
resultRGB <- resultRGB * 255 # R, G and B between 0 and 255

# (3) HSV color of each pixel in the image

library(grDevices)

R.comp <- as.vector(resultRGB[,,1]); G.comp <- as.vector(resultRGB[,,2]); B.comp <- as.vector(resultRGB[,,3])
matrix_RGB <- rbind(R.comp, G.comp, B.comp)
matrix_HSV <- rgb2hsv(matrix_RGB) # help(rgb2hsv): in first argument, 3-row rgb matrix
resultHSV <- array(c(matrix_HSV[1,]*360, matrix_HSV[2,], matrix_HSV[3,]), dim(resultRGB))



# (4) Detection of the yellow pixels in HSV

dim(resultHSV) #  717 pixels in horizontal, 957 pixels in vertical
# Remark. Pixel (1,1) is on the topleft corner
#         Pixel (717, 957) is on the bottomright corner

n1 <- dim(resultHSV)[1] # 717
n2 <- dim(resultHSV)[2] # 957

res.bin.HSV <- matrix(0,n1,n2) # to fill in with a 1 if the pixel is of interest, and with a 0 otherwise

res.bin.HSV[ which( resultHSV[,,1]>50 & resultHSV[,,1]<70 )] <- 1  # 'pure' yellow has Hue=60

# Put a 0 in some regions that have no interest and may cause problems:
res.bin.HSV[1:335,] <- 0   # Top of the image
res.bin.HSV[670:717,] <- 0 # Bottom of the image
res.bin.HSV[482:483,] <- 0 # Horizontal axis (velocity=0)

# Put a zero for the isolated pixels with a yellow tone that are almost black 
# 1. Check if the pixel is almost black (V small, near 0)
# 2. Check if the mean of the V components of the pixels in a neighbourhood is near 0

matrix_neighb_HSV <- function(matr,r,i,j){
  nr <- dim(matr)[1]
  nc <- dim(matr)[2]
  
  if ( ( ( ((i-r) > 0)  & ( (i+r) <= nr ) ) & ( (j-r)>0 ) )  & ( (j+r) <= nc ) ) {
    
    # We do not consider green (ECG) or white (letters) pixels in the neighbourhood, since they would increase the mean of V
    result <- mean(matr[(i-r):(i+r),(j-r):(j+r),3]*(matr[(i-r):(i+r),(j-r):(j+r),1]!=120)*(matr[(i-r):(i+r),(j-r):(j+r),2]!=0))
    
  } else result <- 0 
  return(result)
}


radio <- 9 # change radio if necessary

for (i in 1:n1){
  for (j in 1:n2){
    cond1 <- resultHSV[i,j,3]<0.15 # change threshold if necessary
    cond2 <- matrix_neighb_HSV(resultHSV,r=radio,i=i,j=j)
    if (cond1 & (cond2<0.15)) res.bin.HSV[i,j] <- 0  # change threshold if necessary
  }
}


# Plot of the binary matrix
library(raster); library(rasterVis); library(lattice)
r.c <- raster(res.bin.HSV); r.c <- ratify(r.c)
rat.c <- levels(r.c)[[1]]; rat.c$code <- c(0, 1); levels(r.c) <- rat.c
mapTheme <- rasterTheme(region=c('#000000','#D6F8F7'))
levelplot(r.c, par.settings=mapTheme,scales=list(x=list(at=NULL),y=list(at=NULL)))



# (5) Transformation into a function

# From pixel coordinates to velocities:
X.pos <- 1:957 # position of each pixel
Y.pos <- seq(-20.52632, 42.36842, length=n1)
# Values -20.52632 and 42.36842 have been calculates as follows:
# 0 cm/s (Y=0 axis) ---> pixel with Y=483 
# 5 cm/s --------------> pixel with Y=483+57
# Therefore, 1 pixel = 0.0877193 cm/s
# Thus, pixels of the row 1 would correspond to velocity 42.36842 cm/s
# and pixels in the row 717 to velocity -20.52632 cm/s
# These values have to be recalculated if working with other type of images


mov_mean <- function(x, k){ # moving mean
  n = length(x)
  x.mov.mean = numeric(n)
  x.mov.mean[1:((k-1)/2)]=x[1:((k-1)/2)]
  x.mov.mean[(n-((k-1)/2)):n]=x[(n-((k-1)/2)):n]
  for (i in (1+(k-1)/2):(n-(k-1)/2)){
    x.mov.mean[i]=mean(x[ (i-(k-1)/2) : (i+(k-1)/2) ])
  }
  return(x.mov.mean)
}



lambda = 10 

Y.values.function=numeric(length(X.pos)) 
for (i in 1:length(X.pos)){
  if (sum(res.bin.HSV[,i]==1)>0){
    a = rev(Y.pos)[max(which(res.bin.HSV[,i]==1))]
    b = rev(Y.pos)[min(which(res.bin.HSV[,i]==1))]
    if (a>0) {
      Y.values.function[i] <- b
    } else if (b<0) {
      Y.values.function[i] <- a
    } else {
      Y.values.function[i] <- a * (exp(lambda*(-a)/(b-a)))/(exp(lambda*(-a)/(b-a))+exp(lambda*b/(b-a))) + b * (exp(lambda*(b)/(b-a)))/(exp(lambda*(-a)/(b-a))+exp(lambda*b/(b-a)))
    }
  }
}


function_TDI <- approxfun(X.pos, mov_mean(Y.values.function, k=13)) # Change the value of k if needed



# (6) Extraction and selection of the cycles

X.grid <- seq(1, 957, by=0.01)
x1 <- function_TDI(X.grid)
x2 <- rep(0, length(X.grid))

# function_TDI <- approxfun(X.grid, x1) # uncomment if a biggest grid needs to be considered

# Intersection points of the function and the X axis:
above <- x1 >= x2 
intersect.points <- which(diff(above) == 1) # intersection points corresponding to changes from negative to positive values in the function (A' wave negative, S wave positive)
plot(X.grid, x1, type='l', xlab='Pixel (time)', ylab='Velocity (cm/s)')
abline(v=X.grid[intersect.points], col='red')
n.int.points <- length(intersect.points)

# Choose between manual or automatic selection (comment the not chosen option):

# ********
# MANUAL SELECTION OF THE CYCLE 
intersect.points <- intersect.points[c(2,4,6)] # for example, if the intersection points of interest are the number 2,4 and 6
abline(v=X.grid[intersect.points], col='blue', lwd=3)
n.int.points <- length(intersect.points)
# ********


# ********
# AUTOMATIC SELECTION OF THE CYCLE
# integral.int.points.left <- numeric(n.int.points)
# library(pracma)
# for (i in 1:n.int.points){
#   integral.int.points.left[i] <- integral(function_TDI, (X.grid[intersect.points][i])-30, X.grid[intersect.points][i])
# }
# abline(v=X.grid[intersect.points]-30, col='gray')
# intersect.points <- intersect.points[integral.int.points.left<=0.6*min(integral.int.points.left)]
# n.int.points <- length(intersect.points)
# abline(v=X.grid[intersect.points], col='blue', lwd=3)
# ********



# Change the palette
col.pal <- palette()
col.pal <- c('black', 'blue', 'red', 'green', 'orange', 'purple')
palette(col.pal)


grid.fun <- seq(0,1,by=0.001)
discr.cycle <- rep(NA, length(grid.fun)) # selected cycle discretized in 1001 points

if (length(intersect.points)==1){ # 0 cycles detected, problem!
  
  discr.cycle <- rep(999, length(grid.fun))
  
} else {
  
  if(max(diff(intersect.points))>30000 | min(diff(intersect.points))<15000){ 
    # if the distance between two consecutive intersection points is too big or too little, problem!
    discr.cycle <- rep(999, length(grid.fun))
    
  } else {
    
    if (length(intersect.points)==2){
      plot(seq(0, 1, len=length(X.grid[intersect.points[1]:intersect.points[2]])), x1[intersect.points[1]:intersect.points[2]], type='l', lwd=2, col=2, ylim=c(-15,15), xlab='Cycle', ylab='Velocity (cm/s)'); abline(h=0) 
      cycle.fun <- approxfun(seq(0, 1, len=length(X.grid[intersect.points[1]:intersect.points[2]])), x1[intersect.points[1]:intersect.points[2]])
      rejilla <- seq(0,1,by=0.001)
      discr.cycle <- cycle.fun(grid.fun)
      discr.cycle[c(1,1001)] <- 0 # comment this line if the cycle does not start and/or end in zero velocity
      discr.cycle <- disc.cycle
      
    } else {
      plot(seq(0, 1, len=length(X.grid[intersect.points[1]:intersect.points[2]])), x1[intersect.points[1]:intersect.points[2]], type='l', lwd=2, col=2, ylim=c(-15,15), xlab='Cycle', ylab='Velocity(cm/s)'); abline(h=0) 
      for(i in 1:(n.int.points-2)){
        lines(seq(0, 1, len=length(X.grid[intersect.points[1+i]:intersect.points[i+2]])), x1[intersect.points[i+1]:intersect.points[i+2]], col=i+2, lwd=2)
      }
      
      n.cycles <- n.int.points - 1
      list.cycles <-  vector(mode = "list", length = n.cycles)
      
      for(i in 1:(n.int.points-1)){
        list.cycles[[i]] <- approxfun(seq(0, 1, len=length(X.grid[intersect.points[i]:intersect.points[i+1]])), x1[intersect.points[i]:intersect.points[i+1]])
      }
      
      # Calculate the mean cycle
      # With this purpose, we have to have all the cycles evaluated in the same grid
      
      cycles.grid <- matrix(NA, nrow=n.cycles, ncol=length(grid.fun))
      
      for(i in 1:n.cycles){
        cycles.grid[i,] <- list.cycles[[i]](grid.fun)
      }
      
      cycle.mean <- colMeans(cycles.grid)
      lines(grid.fun, cycle.mean, lwd=2)
      
      i.selected.cycle <- which.min(as.matrix(dist(rbind(cycle.mean, cycles.grid), method='manhattan'))[-1,1])
      
      selected.cycle <- cycles.grid[i.selected.cycle,]
      selected.cycle[c(1,1001)] <- 0 # comment this line if the cycle does not start and/or end in zero velocity
      
      selected.cycle.fun <- approxfun(grid.fun, selected.cycle)
      
      mtext(paste('The selected cycle is the one in', palette()[i.selected.cycle+1]),side=3)
      
      discr.cycle <- selected.cycle
    }
  }
} 

